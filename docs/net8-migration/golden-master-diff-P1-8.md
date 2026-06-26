# P1-8 Golden-Master 逐值 Diff 判定

> net462 ↔ net8.0-windows 雙 TFM 行為對比的結論性證據。資料來源:CI run **28245039239**(windows-latest，`Category=CaptureBaseline` step，雙 TFM 同步跑出 capture log)。
> 本報告是 P1-8「逐值 diff」的 diff-judgment artifact，**砍雙 TFM(P1-8 收尾)前必讀**——它證明 net8 與 net462 的行為等價邊界在哪裡。

## 判定方法

每條 capture 輸出列都自帶 key(token / bits / input+culture)。雙 TFM 在同一 CI step 平行輸出、交錯混在一起；以「**整列文字逐字分組計次**」拆解:

- **count=2** = 該列在 net462 與 net8 各出現一次、逐字相同 → **兩框架一致**。
- **count=1** = 該列只在一個 TFM 出現 → **該值在另一框架不同** → 必為一條 divergence 的其中一腿。

統計:**95 條 distinct 列；79 條 count=2(一致);16 條 count=1**。16 條 count=1 = **8 個 divergent 案例**(每案 2 腿,一腿 net462、一腿 net8),全部落在 2 個**事前已記錄、語意良性**的家族。**零非預期 divergence**。

## Family A — `double.ToString()` 位數漂移(4 案)

net462 用 **G15**(≤15 位有效數字);netcoreapp3.0+/net8 改**最短可往返**表示(常 16–17 位)。**IEEE-754 bit pattern 完全相同**,只是字串渲染位數不同。

| bits | net462(G15) | net8(最短往返) |
|---|---|---|
| `0x3FD5555555555555` | `0.333333333333333` | `0.3333333333333333` |
| `0xBFD5555555555555` | `-0.333333333333333` | `-0.3333333333333333` |
| `0x3FE5555555555555` | `0.666666666666667` | `0.6666666666666666` |
| `0x419D6F34547E6B75` | `123456789.123457` | `123456789.12345679` |

**影響**:`FileProcess.ValidateAndConvertStatisticValue` 的 C 腿(`validatedValue.ToString()`)會把這多出來的位數寫進 MySQL SQL literal。值在數學上相等,但字面字串會變長。屬已知、可接受的格式差(root cause C，見 `NET8_UPGRADE_TEST_STRATEGY.md`)。

## Family B — `"1E400"` 溢位語意翻轉(4 案)

net462 的 `double.TryParse` 把超出範圍的指數解析成 **±∞**;net8 改回 **false(out 0)**。這是雙 TFM 之間**唯一真正的 parse 行為翻轉**。

| 來源 | net462 | net8 |
|---|---|---|
| `SpecialFloatParseTests` token=`1E400` | `parsed=True value=Infinity bits=0x7FF0000000000000` | `parsed=False value=0 bits=0x0000000000000000` |
| `ValidateAndConvert` input=`1E400` culture=Invariant | literal=`Infinity` | literal=`0` |
| `ValidateAndConvert` input=`1E400` culture=en-US | literal=`∞` | literal=`0` |
| `ValidateAndConvert` input=`1E400` culture=zh-TW | literal=`∞` | literal=`0` |

**影響**:若實際測試資料出現超範圍指數字串,net462 會寫進 `Infinity`/`∞`,net8 會寫進 `0`。半導體統計值幾乎不可能出現 `1E400` 量級,風險極低;但這是 cutover 後影子試跑(Q4)要特別盯的一格。

## 經 capture 推翻的事前假設(正向發現:真實 divergence 比預期少)

下列原本預測會 divergence、但 capture 證實**兩框架一致**(count=2),相關測試註解已於本 commit 更正(`SpecialFloatParseTests.cs` / `ValidateAndConvertStatisticValueTests.cs`):

- **Windows-CRT 舊 token `-1.#IND` / `1.#QNAN` / `1.#INF` / `-1.#INF`**:原假設 net462 收(→NaN/±∞)、net8 拒。**實測兩框架皆拒收**(`parsed=False`、out `0`、literal `"0"`)。
- **`AverageOfSumSquare` 餵 `-1.#IND`**:原預測 net462 `count=0`(NaN→OverflowException→空 list)、net8 `count=1`。**實測兩框架皆 `count=1`**(`pass_n=3, avg=2, avg2≈4.667`)——因 net462 同樣拒收該 token、直接跳過。
- **`NaN` / `Infinity` / `-Infinity` 字面 token**:兩框架皆接受且 bit 相同;culture 效應(Invariant/en-US 收、zh-TW 拒)兩框架一致,非 TFM 差異。
- **DateTime / culture 解析**(`06/07/2022` → `2022-06-07 00:00:00` 等):全部 count=2,兩框架一致;來源行 `hh="2022-06-06 01:08:22"` 的 12 小時制既有 bug 亦兩框架一致(非本次遷移引入)。

## 結論

net8.0-windows 與 net462 行為**等價**,僅 8 案差異、全在 2 個事前記錄的良性家族(`double.ToString` 位數、`1E400` 溢位),**零非預期 divergence**。golden-master gate **通過**。

雙 TFM scaffold(L1 rollback)依使用者決策**保留至 cutover 後穩定觀察期**;砍 TFM + docs/codebase + CLAUDE.md 最終同步在該期之後執行,屆時以本報告為等價性依據。
