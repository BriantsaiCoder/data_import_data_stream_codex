#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Text;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// net8 預設編碼 provider 不含 codepage 950(big5)。測試行程不會執行 <c>Program.Main</c>(正式碼的註冊點),
    /// 故 Big5DecodeTests 等直接呼叫 <c>Encoding.GetEncoding("big5")</c> 的測試在測試行程需自行註冊一次,
    /// 與正式碼在 Main 註冊等價。<see cref="ModuleInitializer"/> 保證在任何測試執行前(組件載入時)跑一次。
    /// net462 內建 950、不需此機制,整檔以 <c>#if NET8_0_OR_GREATER</c> 隔離。
    /// </summary>
    internal static class EncodingTestBootstrap
    {
        [ModuleInitializer]
        internal static void RegisterCodePages()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
#endif
