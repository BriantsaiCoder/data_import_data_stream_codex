# 未使用的 NuGet 套件分析

## 分析日期

2025-01-07

## 套件使用情況分析

### ✅ 正在使用的套件

#### 1. MySql.Data 8.4.0

- **檔案**: `MySQL_api\DBmysql.cs`
- **使用**: `using MySql.Data.MySqlClient;`
- **狀態**: ✅ 確實使用中 - 資料庫連線核心套件

#### 2. Dapper 2.1.66

- **檔案**: `MySQL_api\DBmysql.cs`
- **使用**: `using Dapper;`
- **狀態**: ✅ 確實使用中 - ORM 框架

#### 3. Newtonsoft.Json 13.0.3

- **檔案**:
  - `MySQL_api\DBmysql.cs` - `using Newtonsoft.Json.Linq;`
  - `DbApi\DbObject.cs` - `using Newtonsoft.Json.Linq;`
- **狀態**: ✅ 確實使用中 - JSON 處理

### ❌ 未使用的套件

#### 1. Microsoft.AspNet.WebApi.Client 5.2.9

- **檢查結果**:
  - 程式碼中只有註解 `//callWebApi();`
  - 沒有實際的 using 陳述式
  - 沒有 HttpClient 或 WebApi 相關程式碼
- **狀態**: ❌ 未使用 - 可以移除

#### 2. Google.Protobuf 3.25.1

- **檢查結果**:
  - 整個專案中沒有使用 Google.Protobuf
  - 沒有相關的 using 陳述式
  - 可能是 MySql.Data 的依賴項，但應該自動處理
- **狀態**: ❌ 明確引用未使用 - 可以移除

#### 3. BouncyCastle.Cryptography 2.6.1

- **檢查結果**:
  - 整個專案中沒有使用 BouncyCastle
  - 沒有相關的 using 陳述式
  - 可能是 MySql.Data 的依賴項，但應該自動處理
- **狀態**: ❌ 明確引用未使用 - 可以移除

#### 4. Microsoft.Bcl.AsyncInterfaces 9.0.1

- **檢查結果**:
  - 專案中沒有明確使用
  - 這是 .NET Framework 的相容性套件
  - 在 .NET Framework 4.6.2 中可能不需要
- **狀態**: ❓ 可能不需要 - 建議測試後移除

## 建議的清理動作

### 第一階段：安全移除

```xml
<!-- 可以安全移除的套件 -->
<package id="Microsoft.AspNet.WebApi.Client" version="5.2.9" targetFramework="net462" />
```

### 第二階段：依賴項測試

```xml
<!-- 需要測試是否為 MySql.Data 必要依賴項 -->
<package id="Google.Protobuf" version="3.25.1" targetFramework="net462" />
<package id="BouncyCastle.Cryptography" version="2.6.1" targetFramework="net462" />
```

### 第三階段：相容性套件檢查

```xml
<!-- 在 .NET Framework 4.6.2 中可能不需要 -->
<package id="Microsoft.Bcl.AsyncInterfaces" version="9.0.1" targetFramework="net462" />
```

## 實施計劃

### 步驟 1：移除明確未使用的套件

1. 移除 `Microsoft.AspNet.WebApi.Client`
2. 執行建置測試

### 步驟 2：檢查 MySql.Data 依賴項

1. 暫時移除 `Google.Protobuf` 和 `BouncyCastle.Cryptography`
2. 執行建置測試
3. 如果建置失敗，檢查 MySql.Data 是否自動帶入這些依賴項

### 步驟 3：清理相容性套件

1. 暫時移除 `Microsoft.Bcl.AsyncInterfaces`
2. 執行建置和功能測試
3. 如果無問題則永久移除

## 預期結果

移除未使用套件後的 `packages.config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="Dapper" version="2.1.66" targetFramework="net462" />
  <package id="MySql.Data" version="8.4.0" targetFramework="net462" />
  <package id="Newtonsoft.Json" version="13.0.3" targetFramework="net462" />
</packages>
```

## 預期效益

1. **減少套件數量**: 從 7 個減少到 3 個
2. **降低安全風險**: 移除不必要的依賴項
3. **減少部署大小**: 移除未使用的 DLL
4. **簡化維護**: 減少需要追蹤更新的套件數量
5. **提升建置速度**: 減少套件還原時間

## 風險評估

- **低風險**: Microsoft.AspNet.WebApi.Client（完全未使用）
- **中低風險**: Google.Protobuf, BouncyCastle.Cryptography（MySql.Data 可能自動處理）
- **中風險**: Microsoft.Bcl.AsyncInterfaces（相容性套件，需要測試）
