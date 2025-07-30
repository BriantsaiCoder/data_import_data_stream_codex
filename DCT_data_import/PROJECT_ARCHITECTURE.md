# DCT Data Import 專案架構文件

## 📋 專案概述

**專案名稱**: DCT Data Import  
**專案類型**: C# Console Application (.NET Framework 4.6.2)  
**主要功能**: 從 FTP 伺服器自動導入和處理 DCT (Direct Circuit Test) 測試數據到 MySQL 資料庫  
**環境支援**: Development (本機) / Production (伺服器)

## 🏗️ 系統架構

### 核心架構模式

- **三層架構**: Presentation Layer → Business Logic Layer → Data Access Layer
- **模組化設計**: 依功能分組的命名空間
- **工廠模式**: 資料庫連接管理
- **策略模式**: 多種數據導入處理器

### 架構圖

```
┌─────────────────────────────────────────────────────────────┐
│                    DCT Data Import System                   │
├─────────────────────────────────────────────────────────────┤
│  Program.cs (Entry Point)                                  │
│  ├── Configuration Management                              │
│  ├── Main Processing Loop                                  │
│  └── Mode Selection (Tester/TSMC)                         │
├─────────────────────────────────────────────────────────────┤
│  Business Logic Layer                                      │
│  ├── ReadAndImport/ (數據處理模組)                         │
│  │   ├── ImportData (基底類別)                            │
│  │   ├── RawData (原始測試數據)                           │
│  │   ├── TsmcIeda (TSMC IEDA 數據)                       │
│  │   ├── RecoveryRate (恢復率數據)                        │
│  │   ├── Tester (測試器狀態)                              │
│  │   ├── FailPin (故障針腳)                               │
│  │   └── UiStatus (UI 狀態)                              │
│  ├── FileAccess/ (檔案處理模組)                            │
│  │   ├── FileProcess (檔案處理核心)                       │
│  │   ├── FileContentFormat (檔案格式定義)                 │
│  │   └── ReadWriteINIfile (INI 檔案操作)                 │
│  └── Support Modules                                       │
│      ├── CalculateSPC (統計製程控制)                       │
│      ├── EmailModels (郵件功能)                            │
│      └── WriteToLog (日誌記錄)                             │
├─────────────────────────────────────────────────────────────┤
│  Data Access Layer                                         │
│  ├── DbApi/ (資料庫API模組)                                │
│  │   ├── DatabaseService (資料庫服務)                      │
│  │   ├── DbAccess (資料庫存取)                             │
│  │   └── DbObject (資料物件定義)                           │
│  └── MySQL_api/ (MySQL專用API)                            │
│      └── DBmysql (MySQL連接與操作)                         │
├─────────────────────────────────────────────────────────────┤
│  External Dependencies                                      │
│  ├── FTP Server (數據源)                                   │
│  ├── MySQL Database (數據存儲)                             │
│  └── Configuration Files (App.config)                     │
└─────────────────────────────────────────────────────────────┘
```

## 📁 專案結構

### 目錄結構

```
DCT_data_import/
├── Program.cs                     # 程式進入點
├── App.config                     # 組態設定
├── packages.config                # NuGet 套件
├── DbApi/                         # 資料庫API模組
│   ├── DatabaseService.cs         # 資料庫服務層
│   ├── DbAccess.cs                # 資料庫存取層
│   └── DbObject.cs                # 資料物件定義
├── MySQL_api/                     # MySQL專用API
│   └── DBmysql.cs                 # MySQL連接與操作
├── ReadAndImport/                 # 數據讀取與導入模組
│   ├── ImportData.cs              # 導入基底類別
│   ├── RawData.cs                 # 原始測試數據處理
│   ├── TsmcIeda.cs                # TSMC IEDA數據處理
│   ├── RecoveryRate.cs            # 恢復率數據處理
│   ├── Tester.cs                  # 測試器狀態處理
│   ├── FailPin.cs                 # 故障針腳處理
│   └── UiStatus.cs                # UI狀態處理
├── FileAccess/                    # 檔案存取模組
│   ├── FileProcess.cs             # 檔案處理核心
│   ├── FileContentFormat.cs       # 檔案格式定義
│   └── ReadWriteINIfile.cs        # INI檔案操作
├── CalculateSPC.cs                # 統計製程控制
├── EmailModels.cs                 # 郵件功能模型
├── WriteToLog.cs                  # 日誌記錄
└── Properties/
    └── AssemblyInfo.cs            # 組件資訊
```

## 🔧 核心元件

### 1. Program.cs - 程式進入點

```csharp
namespace DCT_data_import
{
    class Program
    {
        // 環境變數與配置
        public static string Environment = GetEnvironment();
        public static string HOST, USER, PASSWORD, PORT, DATABASE;
        public static string FTP_IP, FTP_USER, FTP_PASSWORD;

        // 主要方法
        static void Main(string[] args)           // 程式進入點
        static string ImportTesterMode(...)      // Tester模式導入
        static string ImportTsmcMode(...)        // TSMC模式導入
        static bool createPool(...)              // 建立資料庫連接池
        static string GetEnvironment()           // 取得環境設定
    }
}
```

### 2. 資料庫存取層 (DbApi/)

#### DatabaseService.cs - 資料庫服務

```csharp
public class DatabaseService
{
    // 資料庫連接與服務管理
    public bool checkDBConnect(string poolName)          // 檢查資料庫連接
    public Execute_query_response ExecuteQuery(...)      // 執行查詢
    public Task<Execute_query_response> ExecuteQueryAsync(...) // 非同步查詢
}
```

#### DbObject.cs - 資料物件

```csharp
public class DbObject
{
    public class ImportResult                    // 導入結果
    public class DbKeyObject                     // 資料庫鍵物件
    public class Execute_query                   // 查詢物件
    public class Execute_query_response          // 查詢回應物件
}
```

#### DBmysql.cs - MySQL 操作

```csharp
public class DBmysql
{
    public void Connect(...)                     // 建立連接
    public Execute_query_response Excute_mysql_cmd(...) // 執行MySQL命令
    private void ExecuteSelectCommand(...)       // 執行SELECT
    private void ExecuteNonQueryCommand(...)     // 執行非查詢命令
}

public class MySqlConnectionManager              // 連接管理器
{
    public static void Initialize(...)           // 初始化連接
    public static bool TestConnection()          // 測試連接
}
```

### 3. 數據處理層 (ReadAndImport/)

#### ImportData.cs - 基底類別

```csharp
public class ImportData
{
    protected long GetFileSize(...)              // 取得檔案大小
    public string DeleteFile(...)                // 刪除FTP檔案
    // FTP操作的共用方法
}
```

#### RawData.cs - 原始測試數據

```csharp
public class RawData : ImportData
{
    public async Task<ImportResult> ReadAndImportRawData(...) // 讀取並導入原始數據
    private RawDataContentFormat FileReadRawData(...)        // 讀取原始數據檔案
    // 統計製程控制計算
    // 資料庫匯入邏輯
}
```

#### TsmcIeda.cs - TSMC IEDA 數據

```csharp
public class TsmcIeda : ImportData
{
    private readonly DataTable _lotMappingDt      // 批次對映表
    public ImportResult ReadAndImportIeda(...)    // 讀取並導入IEDA數據
    public IedaDataFormat FileReadIeda(...)       // 讀取IEDA檔案
    // TSMC特定的資料處理邏輯
}
```

#### RecoveryRate.cs - 恢復率數據

```csharp
public class RecoveryRate : ImportData
{
    public async Task<ImportResult> ReadAndImportRecoveryRateData(...) // 恢復率數據處理
    private RecoveryRateDataContentFormat FileReadRecoveryRateData(...) // 讀取恢復率檔案
    // 恢復率計算與分析
}
```

#### Tester.cs - 測試器狀態

```csharp
public class Tester : ImportData
{
    public async Task<ImportResult> ReadAndImportTesterStatus(...) // 測試器狀態處理
    private TesterContentFormat FileReadTesterStatus(...)          // 讀取測試器狀態檔案
    // 測試器狀態監控與記錄
}
```

### 4. 檔案處理層 (FileAccess/)

#### FileProcess.cs - 檔案處理核心

```csharp
public class FileProcess
{
    // 資料庫匯入方法
    public Execute_query_response ExecuteInsertWithAPI(...)    // API方式插入
    public Execute_query_response ExecuteUpdateWithAPI(...)    // API方式更新
    public Execute_query_response ExecuteSelectWithAPI(...)    // API方式查詢

    // 檔案格式驗證
    public bool CheckDataTableColumn(...)                      // 檢查資料表欄位

    // 專用匯入方法
    public bool ImportUIStatus(...)                            // 匯入UI狀態
    public bool ImportRawData(...)                             // 匯入原始數據
    public bool ImportRecoveryRateData(...)                    // 匯入恢復率數據
    public bool ImportTesterStatus(...)                        // 匯入測試器狀態
    public bool ImportIedaTitle(...)                           // 匯入IEDA標題
    public bool ImportIedaContent(...)                         // 匯入IEDA內容
}
```

#### FileContentFormat.cs - 檔案格式定義

```csharp
// 各種檔案格式的資料結構定義
public class RecoveryRateDataContentFormat    // 恢復率檔案格式
public class RawDataContentFormat             // 原始數據檔案格式
public class TesterContentFormat              // 測試器檔案格式
public class UIStatusContentFormat            // UI狀態檔案格式
public class IedaDataFormat                   // IEDA數據檔案格式
```

### 5. 支援模組

#### CalculateSPC.cs - 統計製程控制

```csharp
public class CalculateSPC
{
    // 統計計算相關方法
    public List<StatisticItem> Calculate(...)    // 統計計算
    // SPC (Statistical Process Control) 相關演算法
}
```

#### WriteToLog.cs - 日誌記錄

```csharp
public class WriteToLog
{
    // 日誌記錄功能
    public void WriteLog(...)                     // 寫入日誌
    // 錯誤追蹤與記錄
}
```

## ⚙️ 技術規格

### 開發環境

- **Framework**: .NET Framework 4.6.2
- **Language**: C# 7.3
- **IDE**: Visual Studio 2019+
- **Target Platform**: Windows

### 核心依賴套件

```xml
<packages>
  <package id="Dapper" version="2.1.66" />                              <!-- ORM框架 -->
  <package id="MySql.Data" version="9.4.0" />                           <!-- MySQL連接器 -->
  <package id="Newtonsoft.Json" version="13.0.3" />                     <!-- JSON處理 -->
  <package id="System.Configuration.ConfigurationManager" version="9.0.7" /> <!-- 配置管理 -->
</packages>
```

### 外部系統整合

1. **FTP 伺服器**: 數據檔案來源
2. **MySQL 資料庫**: 數據持久化存儲
3. **API 服務**: HTTP API 介面 (http://10.16.93.46:3001/)

## 🔄 數據流程

### 主要處理流程

```
1. 程式啟動 → 讀取配置 → 建立資料庫連接池
2. 查詢待處理的 DB Key 清單
3. 根據 CheckStatus 決定處理模式:
   ├── Tester Mode: 處理測試器相關數據
   │   ├── RecoveryRate (恢復率)
   │   ├── RawData (原始測試數據)
   │   ├── Tester Status (測試器狀態)
   │   └── FailPin (故障針腳)
   └── TSMC Mode: 處理TSMC IEDA數據
4. 從 FTP 下載對應檔案
5. 解析檔案內容並驗證格式
6. 計算統計數據 (如需要)
7. 匯入資料庫
8. 更新處理狀態
9. 清理臨時檔案
10. 記錄處理結果
```

### CheckStatus 狀態管理

```
CheckStatus 值決定處理類型:
- 1,3,5,7,9,11,13,15: RecoveryRate 處理
- 2,3,6,7,10,11,14,15: RawData 處理
- 4,5,6,7,12,13,14,15: Tester Status 處理
- 8,9,10,11,12,13,14,15: FailPin 處理
```

## 🗄️ 資料庫架構

### 主要資料表

1. **db_key_list**: DB Key 管理表
2. **recovery_rate_data**: 恢復率數據表
3. **raw_data**: 原始測試數據表
4. **tester_status**: 測試器狀態表
5. **fail_pin_data**: 故障針腳數據表
6. **ieda_title**: IEDA 標題數據表
7. **ieda_content**: IEDA 內容數據表
8. **ui_status**: UI 狀態數據表

### 資料庫連接配置

```xml
<!-- Development 環境 -->
<add key="DevHost" value="localhost" />
<add key="DevUserName" value="root" />
<add key="DevPassword" value="root" />

<!-- Production 環境 -->
<add key="ProdHost" value="10.16.92.67" />
<add key="ProdUserName" value="5910" />
<add key="ProdPassword" value="TID_5910!" />
```

## 🔒 安全性考量

### 配置安全

- 敏感資訊存儲在 App.config
- 資料庫密碼加密保護
- FTP 認證憑證安全管理

### 資料完整性

- 檔案格式驗證
- 資料庫交易完整性
- 錯誤回滾機制

### 存取控制

- 資料庫連接池管理
- FTP 檔案存取權限
- 日誌記錄與審計

## 📈 效能優化

### 非同步處理

- 使用 `async/await` 處理檔案 I/O
- 非同步資料庫操作
- 平行處理多個 DB Key

### 記憶體管理

- 大檔案分塊讀取
- 及時釋放資源
- 資料庫連接池管理

### 網路優化

- FTP 檔案壓縮傳輸
- 批次資料庫操作
- 連接重用策略

## 🐛 錯誤處理

### 異常處理策略

1. **檔案處理異常**: 重試機制
2. **網路連接異常**: 自動重連
3. **資料庫異常**: 交易回滾
4. **數據格式異常**: 記錄並跳過

### 日誌系統

- 詳細的錯誤追蹤
- 效能監控記錄
- 處理狀態追蹤

## 🚀 部署與維護

### 部署需求

- Windows Server 環境
- .NET Framework 4.6.2+
- MySQL 8.0+ 客戶端
- 網路存取權限 (FTP/MySQL)

### 監控指標

- 處理成功率
- 平均處理時間
- 錯誤發生頻率
- 資源使用狀況

### 維護任務

- 日誌檔案清理
- 資料庫效能優化
- 配置檔案更新
- 依賴套件升級

---

**版本**: 3.2.0.0  
**最後更新**: 2025-07-26  
**維護者**: DCT Data Import Team
