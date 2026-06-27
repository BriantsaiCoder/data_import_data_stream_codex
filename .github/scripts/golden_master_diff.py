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
from datetime import date

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
    with open(path, encoding="utf-8", errors="replace") as fh:
        for raw in fh:
            line = raw.rstrip("\n").strip()
            if not is_payload(line):
                continue
            if is_tolerated(line, run_day):
                continue
            kept.append(line)
    return sorted(kept)


def main():
    if len(sys.argv) != 3:
        print("usage: golden_master_diff.py <net462-capture.log> <net8-capture.log>", file=sys.stderr)
        return 2
    run_day = date.today().isoformat()
    net462 = load(sys.argv[1], run_day)
    net8 = load(sys.argv[2], run_day)

    only_462 = [l for l in net462 if l not in net8]
    only_8 = [l for l in net8 if l not in net462]

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
