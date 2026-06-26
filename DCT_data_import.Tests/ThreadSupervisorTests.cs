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

        /// <summary>
        /// 釘住 <see cref="Program.RestartWorker"/> 在<b>首次啟動路徑</b>的契約:Main 第一輪迴圈時三條
        /// worker 是「已 new、尚未 Start」(<see cref="ThreadState.Unstarted"/>),supervisor 偵測
        /// <c>alive==false</c> 後就把這條 unstarted thread 餵給 RestartWorker。
        ///
        /// <para>為何單獨測 Unstarted(而非沿用上面的 dead/Stopped 案例):兩者是不同 <see cref="ThreadState"/>,
        /// 且 Unstarted 才是初次啟動的真實狀態——若這條路徑斷裂,ETL 連第一輪都起不來。Copilot review 曾質疑
        /// 對 unstarted thread 呼叫 <c>current.Interrupt()</c> 會擲 <see cref="ThreadStateException"/> → 被
        /// catch 吞掉 → 永遠回原 thread、新 worker 不啟動。但 MS API 文件 <c>Thread.Interrupt</c> 的例外清單
        /// 只列 <c>SecurityException</c>(對照 <c>Thread.Join</c> 明列「thread has not been started」的
        /// ThreadStateException),故對 unstarted thread 呼叫 Interrupt 是 no-op(設 pending interrupt 旗標,
        /// thread 從未 block 故永不觸發),不擲例外 → 一定走到 <c>fresh.Start()</c>。本測試把此契約轉成可執行
        /// 證據,雙 TFM(net462/net8)CI 皆應綠。</para>
        /// </summary>
        [Fact]
        public void RestartWorker_UnstartedThread_StillCreatesAndStartsNewThread()
        {
            // arrange:已 new、未 Start 的 thread,重現 Main 首次迴圈餵進 supervisor 的真實狀態
            Thread unstarted = new Thread(() => { });
            Assert.Equal(ThreadState.Unstarted, unstarted.ThreadState);

            var newWorkerRan = new ManualResetEventSlim(false);

            // act
            Thread result = Program.RestartWorker(
                unstarted, () => newWorkerRan.Set(), new WriteToLog(), "Test");

            // assert:Interrupt() 對 unstarted thread 不擲例外 → 必須建立並啟動一條新 thread
            Assert.NotSame(unstarted, result);
            Assert.True(newWorkerRan.Wait(TimeSpan.FromSeconds(2)),
                "對 unstarted thread 呼叫 RestartWorker 後新 worker 未執行 → 首次啟動路徑斷裂,ETL 起不來。");
        }
    }
}
