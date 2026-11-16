using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace YFrame.Service
{
    /// <summary>
    /// 音频设备测试服务：使用 Windows winmm 默认播放/录音设备，
    /// 输出测试播放 1 秒 440Hz 正弦波，输入测试录音 1.5 秒并返回峰值电平。
    /// 不依赖第三方音频库。
    /// </summary>
    public static class AudioTestService
    {
        private const uint WAVE_MAPPER = 0xFFFFFFFF;
        private const int SAMPLE_RATE = 44100;
        private const int CHANNELS = 1;
        private const int BITS_PER_SAMPLE = 16;

        /// <summary>WAVE 格式结构</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        /// <summary>WAVE 缓冲区头结构</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int waveOutOpen(out IntPtr hWaveOut, uint uDeviceID, ref WAVEFORMATEX lpFormat,
            IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);
        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hWaveOut);
        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int waveInOpen(out IntPtr hWaveIn, uint uDeviceID, ref WAVEFORMATEX lpFormat,
            IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);
        [DllImport("winmm.dll")]
        private static extern int waveInPrepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);
        [DllImport("winmm.dll")]
        private static extern int waveInAddBuffer(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);
        [DllImport("winmm.dll")]
        private static extern int waveInStart(IntPtr hWaveIn);
        [DllImport("winmm.dll")]
        private static extern int waveInStop(IntPtr hWaveIn);
        [DllImport("winmm.dll")]
        private static extern int waveInReset(IntPtr hWaveIn);
        [DllImport("winmm.dll")]
        private static extern int waveInUnprepareHeader(IntPtr hWaveIn, IntPtr lpWaveInHdr, int uSize);
        [DllImport("winmm.dll")]
        private static extern int waveInClose(IntPtr hWaveIn);

        /// <summary>
        /// 通过默认输出设备播放 1 秒测试音。
        /// </summary>
        /// <param name="error">失败时的错误说明</param>
        /// <returns>是否播放成功</returns>
        public static bool PlayTestTone(out string error)
        {
            error = string.Empty;
            IntPtr data = IntPtr.Zero;
            IntPtr header = IntPtr.Zero;
            IntPtr device = IntPtr.Zero;
            bool prepared = false;

            try
            {
                int sampleCount = SAMPLE_RATE;
                short[] samples = new short[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    // 首尾加淡入淡出，避免扬声器出现爆音
                    double fade = Math.Min(1.0, Math.Min(i, sampleCount - i) / (SAMPLE_RATE * 0.02));
                    samples[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / SAMPLE_RATE) * 0.20 * short.MaxValue * fade);
                }

                int bytes = samples.Length * sizeof(short);
                data = Marshal.AllocHGlobal(bytes);
                Marshal.Copy(samples, 0, data, samples.Length);

                var headerData = new WAVEHDR { lpData = data, dwBufferLength = (uint)bytes };
                header = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
                Marshal.StructureToPtr(headerData, header, false);

                var format = CreateFormat();
                int rc = waveOutOpen(out device, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                if (rc != 0)
                {
                    error = $"无法打开默认输出设备（winmm={rc}）";
                    return false;
                }

                rc = waveOutPrepareHeader(device, header, Marshal.SizeOf<WAVEHDR>());
                if (rc != 0)
                {
                    error = $"输出缓冲区准备失败（winmm={rc}）";
                    return false;
                }
                prepared = true;

                rc = waveOutWrite(device, header, Marshal.SizeOf<WAVEHDR>());
                if (rc != 0)
                {
                    error = $"测试音播放失败（winmm={rc}）";
                    return false;
                }

                // 等待 1 秒音频播放完成，留 150ms 收尾
                Thread.Sleep(1150);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (device != IntPtr.Zero)
                {
                    waveOutReset(device);
                    if (prepared && header != IntPtr.Zero)
                        waveOutUnprepareHeader(device, header, Marshal.SizeOf<WAVEHDR>());
                    waveOutClose(device);
                }
                if (header != IntPtr.Zero) Marshal.FreeHGlobal(header);
                if (data != IntPtr.Zero) Marshal.FreeHGlobal(data);
            }
        }

        /// <summary>
        /// 通过默认输入设备录音 1.5 秒并计算峰值电平（0~1）。
        /// </summary>
        /// <param name="peak">峰值电平，0 表示无声或失败</param>
        /// <param name="error">失败时的错误说明</param>
        /// <returns>是否录音成功</returns>
        public static bool RecordPeak(out double peak, out string error)
        {
            peak = 0;
            error = string.Empty;
            IntPtr data = IntPtr.Zero;
            IntPtr header = IntPtr.Zero;
            IntPtr device = IntPtr.Zero;
            bool prepared = false;

            try
            {
                int sampleCount = (int)(SAMPLE_RATE * 1.5);
                int bytes = sampleCount * sizeof(short);
                data = Marshal.AllocHGlobal(bytes);
                // 显式清零，未录满时剩余部分不会被误判为信号
                for (int i = 0; i < bytes; i++) Marshal.WriteByte(data, i, 0);

                var headerData = new WAVEHDR { lpData = data, dwBufferLength = (uint)bytes };
                header = Marshal.AllocHGlobal(Marshal.SizeOf<WAVEHDR>());
                Marshal.StructureToPtr(headerData, header, false);

                var format = CreateFormat();
                int rc = waveInOpen(out device, WAVE_MAPPER, ref format, IntPtr.Zero, IntPtr.Zero, 0);
                if (rc != 0)
                {
                    error = $"无法打开默认输入设备（winmm={rc}）";
                    return false;
                }

                rc = waveInPrepareHeader(device, header, Marshal.SizeOf<WAVEHDR>());
                if (rc != 0)
                {
                    error = $"输入缓冲区准备失败（winmm={rc}）";
                    return false;
                }
                prepared = true;

                rc = waveInAddBuffer(device, header, Marshal.SizeOf<WAVEHDR>());
                if (rc != 0)
                {
                    error = $"输入缓冲区挂载失败（winmm={rc}）";
                    return false;
                }

                rc = waveInStart(device);
                if (rc != 0)
                {
                    error = $"录音启动失败（winmm={rc}）";
                    return false;
                }

                Thread.Sleep(1600);
                waveInStop(device);
                waveInReset(device);

                var recorded = Marshal.PtrToStructure<WAVEHDR>(header);
                int recordedBytes = (int)Math.Min(recorded.dwBytesRecorded, (uint)bytes);
                if (recordedBytes <= 0)
                {
                    error = "未采集到输入数据";
                    return false;
                }

                short[] samples = new short[recordedBytes / sizeof(short)];
                Marshal.Copy(data, samples, 0, samples.Length);

                int max = 0;
                foreach (short sample in samples)
                    max = Math.Max(max, Math.Abs((int)sample));

                peak = max / (double)short.MaxValue;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (device != IntPtr.Zero)
                {
                    waveInStop(device);
                    waveInReset(device);
                    if (prepared && header != IntPtr.Zero)
                        waveInUnprepareHeader(device, header, Marshal.SizeOf<WAVEHDR>());
                    waveInClose(device);
                }
                if (header != IntPtr.Zero) Marshal.FreeHGlobal(header);
                if (data != IntPtr.Zero) Marshal.FreeHGlobal(data);
            }
        }

        /// <summary>创建 44.1kHz / 16bit / 单声道 PCM 格式</summary>
        private static WAVEFORMATEX CreateFormat() => new()
        {
            wFormatTag = 1,
            nChannels = CHANNELS,
            nSamplesPerSec = SAMPLE_RATE,
            nAvgBytesPerSec = SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8),
            nBlockAlign = (ushort)(CHANNELS * (BITS_PER_SAMPLE / 8)),
            wBitsPerSample = BITS_PER_SAMPLE,
            cbSize = 0
        };
    }
}
