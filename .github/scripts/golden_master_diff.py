#!/usr/bin/env python3
"""net462 ↔ net8.0-windows golden-master 逐值 diff（fail-on-diff gate）。

phase-1-migration.md P1-8 與 golden-master-diff-P1-8.md 的 CI 自動化版：
把同一套 CaptureBaseline 測試在兩個 TFM 各自跑出的 capture log 逐列比對,
扣掉「事前已記錄、語意良性」的 8 條 divergence 與 DateTimeParser 的 Now fallback 非決定性列後,
殘餘任何差異 = 未預期回歸 → exit 1。

容忍清單來源唯一:golden-master-diff-P1-8.md(Family A double.ToString 位數漂移 ×4、
Family B "1E400" 溢位語意翻轉 ×4)。容忍鍵一律綁「穩定輸入身分」(bits / token / input),
不綁輸出值——輸出值正是兩框架合法相異之處,綁它會把真回歸也一起放行。
"""
import re
import sys
from collections import Counter
from datetime import date

# Windows runner 的 pwsh 把 Python stdout codec 設成 cp1252,繁中診斷訊息會 UnicodeEncodeError 崩潰(exit 1)。
# 強制 stdout/stderr 走 UTF-8,讓 PASS/FAIL 訊息在任何 runner locale 都印得出(CI 首跑即炸於此)。
for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

# capture payload 列的辨識鍵:只有含這些 token 之一的列才是測試印出的值,
# 其餘(dotnet/xunit 進度、摘要、空行)一律不參與 diff。
PAYLOAD_KEYS = ("token=", "bits=", "input=", "source=", "item_count=", "item[")

# Family A(double.ToString G15 ↔ 最短往返):這 4 個 IEEE-754 bit pattern 的渲染列
# 在 net462/net8 合法相異(位數不同、bit 相同)。以 bits 值為穩定鍵整列放行。
FAMILY_A_BITS = (
    "0x3FD5555555555555",
    "0xBFD5555555555555",
    "0x3FE5555555555555",
    "0x419D6F34547E6B75",
)

# Family B("1E400" 溢位:net462→±∞ / net8→false,0)。涵蓋 SpecialFloatParse 的
# token="1E400" 與 ValidateAndConvert 的 input="1E400"(三 culture)。以輸入字面為穩定鍵放行。
FAMILY_B_MARKERS = ('token="1E400"', 'input="1E400"')

# DateTimeParserCultureTests 解析失敗時回 DateTime.Now(非決定性);兩 TFM 各別跑、
# 時鐘差會讓該列無謂相異。判讀法同測試註解:result 內日期 == 跑日 = Now fallback,排除。
RESULT_LINE = re.compile(r'\bresult="(\d{4}-\d{2}-\d{2})\b')


def is_payload(line):
    return any(k in line for k in PAYLOAD_KEYS)


def is_tolerated(line, run_day):
    if any(b in line for b in FAMILY_A_BITS) and "default=" in line:
        return True
    if any(m in line for m in FAMILY_B_MARKERS):
        return True
    m = RESULT_LINE.search(line)
    if m and m.group(1) == run_day:
        return True
    return False


def load(path, run_day):
    kept = []
    raw_payload = 0  # 容忍過濾前的 payload 列數,供空集合守衛判斷 capture 是否真有內容
    # utf-8-sig:Windows pwsh `>` 重導可能寫入 UTF-8 BOM,sig 去 BOM、無 BOM 時行為同 utf-8。
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        for raw in fh:
            line = raw.rstrip("\n").strip()
            if not is_payload(line):
                continue
            raw_payload += 1
            if is_tolerated(line, run_day):
                continue
            kept.append(line)
    return sorted(kept), raw_payload


def main():
    if len(sys.argv) != 3:
        print("usage: golden_master_diff.py <net462-capture.log> <net8-capture.log>", file=sys.stderr)
        return 2
    run_day = date.today().isoformat()
    net462, raw462 = load(sys.argv[1], run_day)
    net8, raw8 = load(sys.argv[2], run_day)

    # 空集合守衛:CaptureBaseline 測試必印多列 payload,任一邊 0 列代表 capture 步驟失敗、
    # log 編碼不對盤或 PAYLOAD_KEYS 不匹配——此時下方 diff 會「兩邊皆空 → 假綠」,故在此攔成 loud FAIL。
    if raw462 == 0 or raw8 == 0:
        print(
            f"golden-master diff: FAIL — capture log 無任何 payload 列(net462={raw462} net8={raw8});"
            " capture 失敗 / 編碼不對盤 / PAYLOAD_KEYS 不匹配,拒絕空集合假綠。",
            file=sys.stderr,
        )
        return 1

    # multiset 差(Counter)而非 list membership:`l not in net8` 會把「net462 印 2 次、net8 印 1 次」
    # 誤判一致;Counter 差能抓到重複列計次不一致(golden-master-diff-P1-8.md 按整列計次判讀 count=1/2)。
    c462, c8 = Counter(net462), Counter(net8)
    only_462 = sorted((c462 - c8).elements())
    only_8 = sorted((c8 - c462).elements())

    if not only_462 and not only_8:
        print(f"golden-master diff: PASS — {len(net462)} 列比對一致(已扣除 8 條良性 divergence 與 Now fallback)")
        return 0

    print("golden-master diff: FAIL — 偵測到未預期 net462↔net8 行為差異(非容忍清單內):", file=sys.stderr)
    for l in only_462:
        print(f"  只在 net462: {l}", file=sys.stderr)
    for l in only_8:
        print(f"  只在 net8  : {l}", file=sys.stderr)
    print(
        "若確為新的良性差異,須先更新 docs/net8-migration/golden-master-diff-P1-8.md 容忍清單再放行。",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
