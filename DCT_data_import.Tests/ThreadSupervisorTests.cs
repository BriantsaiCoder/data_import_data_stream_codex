using System;
using System.Threading;
using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    /// <summary>
    /// Root cause(net8 升級閘門):<c>Thread.Abort()</c> 在 net5+ 為 SYSLIB0006 obsolete,執行期無條件擲
    /// <see cref="PlatformNotSupportedException"/>(非 <c>ThreadAbortException</c>)。原 supervisor 在
    /// <c>if (!alive)</c> 區塊內依序 <c>Interrupt() → Abort() → new Thread() → Start()</c>;net8 上 <c>Abort()</c>
    /// 一擲 PNSE,後面的 <c>new Thread()/Start()</c> 就被同區塊的 <c>catch(Exception)</c> 跳過 → 死掉的 worker
    /// 永遠不會重建 → ETL 靜默停擺。
    ///
    /// <para>本測試釘住 supervisor 的核心不變條件:「偵測到 worker 已死 → 必須重建並啟動一條新 thread」。
    /// 在含 <c>Abort()</c> 的版本上(net8)會先紅(回傳的仍是舊死 thread、新工作未跑);移除 <c>Abort()</c> 後轉綠。
    /// 此邏輯內嵌於 <c>Main</c> 無法直接測,故抽出 <see cref="Program.RestartWorker"/> seam(與 R5
    /// <c>ComputeImportResult</c> 同模式)。</para>
    /// </summary>
    public class ThreadSupervisorTests
    {
        [Fact]
        public void RestartWorker_DeadThread_CreatesAndStartsNewThread()
        {
            // arrange:造一條已執行完畢(IsAlive=false)的 thread,模擬 supervisor 偵測到的死 worker
            Thread dead = new Thread(() => { });
            dead.Start();
            dead.Join();
            Assert.False(dead.IsAlive);

            var newWorkerRan = new ManualResetEventSlim(false);

            // act
            Thread result = Program.RestartWorker(
                dead, () => newWorkerRan.Set(), new WriteToLog(), "Test");

            // assert:必須是一條「新」thread(非原死 thread),且新工作確實被執行
            Assert.NotSame(dead, result);
            Assert.True(newWorkerRan.Wait(TimeSpan.FromSeconds(2)),
                "重建的 worker thread 未在時限內執行 → supervisor 未成功重啟死掉的 worker。");
        }
    }
}
