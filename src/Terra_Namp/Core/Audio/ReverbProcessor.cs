using System;

namespace Terra_Namp.Core.Audio;

/// <summary>
/// Freeverb-style reverb with pre-delay + high-pass filtered input.
/// Pre-delay (25ms) separates dry vocal attack from reverb onset.
/// Biquad HPF (200Hz) on reverb input removes low-frequency muddiness.
/// Lowpass-feedback comb filters + allpass diffusers for diffuse tail.
/// Operates in-place on stereo interleaved 16-bit PCM at 40kHz.
/// </summary>
public class ReverbProcessor
{
    // Freeverb comb delays (Jezar values scaled from 44.1kHz to 40kHz, +23 stereo spread for R)
    private static readonly int[] CombDelaysL = { 1283, 1371, 1429, 1493, 1549, 1613, 1663, 1733 };
    private static readonly int[] CombDelaysR = { 1306, 1394, 1452, 1516, 1572, 1636, 1686, 1756 };
    private const int NumCombs = 8;

    // Freeverb allpass delays (scaled to 40kHz, +23 stereo spread for R)
    private static readonly int[] AllpassDelaysL = { 480, 161, 45 };
    private static readonly int[] AllpassDelaysR = { 503, 184, 68 };
    private const int NumAllpass = 3;

    private const float Feedback = 0.82f;
    private const float Damp = 0.35f;
    private const float DampInv = 1f - Damp;
    private const float AllpassGain = 0.5f;

    private const float WetMix = 0.234f;
    private const float DryMix = 0.85f;

    // Pre-delay: 25ms at 40kHz = 1000 samples per channel
    private const int PreDelaySamples = 1000;
    private readonly float[] preDelayBufL = new float[PreDelaySamples];
    private readonly float[] preDelayBufR = new float[PreDelaySamples];
    private int preDelayIdx;

    // Biquad high-pass filter coefficients: 200Hz cutoff, Butterworth (Q=0.707), 40kHz sample rate
    // Removes bass from reverb input — keeps low end clean in dry signal only
    private const float HpB0 = 0.97802f;
    private const float HpB1 = -1.95603f;
    private const float HpB2 = 0.97802f;
    private const float HpA1 = -1.95559f;
    private const float HpA2 = 0.95649f;

    // Biquad state per channel (Direct Form I)
    private float hpX1L, hpX2L, hpY1L, hpY2L;
    private float hpX1R, hpX2R, hpY1R, hpY2R;

    private readonly float[][] combBufL, combBufR;
    private readonly int[] combIdxL, combIdxR;
    private readonly float[] combFilterL, combFilterR;

    private readonly float[][] apBufL, apBufR;
    private readonly int[] apIdxL, apIdxR;

    public ReverbProcessor()
    {
        combBufL = new float[NumCombs][];
        combBufR = new float[NumCombs][];
        combIdxL = new int[NumCombs];
        combIdxR = new int[NumCombs];
        combFilterL = new float[NumCombs];
        combFilterR = new float[NumCombs];

        for (int i = 0; i < NumCombs; i++)
        {
            combBufL[i] = new float[CombDelaysL[i]];
            combBufR[i] = new float[CombDelaysR[i]];
        }

        apBufL = new float[NumAllpass][];
        apBufR = new float[NumAllpass][];
        apIdxL = new int[NumAllpass];
        apIdxR = new int[NumAllpass];

        for (int i = 0; i < NumAllpass; i++)
        {
            apBufL[i] = new float[AllpassDelaysL[i]];
            apBufR[i] = new float[AllpassDelaysR[i]];
        }
    }

    public void Process(Span<short> samples)
    {
        for (int i = 0; i < samples.Length - 1; i += 2)
        {
            float inL = samples[i] / 32768f;
            float inR = samples[i + 1] / 32768f;

            // --- Pre-delay: 25ms separation between dry signal and reverb onset ---
            float delayedL = preDelayBufL[preDelayIdx];
            float delayedR = preDelayBufR[preDelayIdx];
            preDelayBufL[preDelayIdx] = inL;
            preDelayBufR[preDelayIdx] = inR;
            preDelayIdx = (preDelayIdx + 1) % PreDelaySamples;

            // --- Biquad HPF: remove <200Hz from reverb input (bass stays in dry only) ---
            float hpL = HpB0 * delayedL + HpB1 * hpX1L + HpB2 * hpX2L
                      - HpA1 * hpY1L - HpA2 * hpY2L;
            hpX2L = hpX1L; hpX1L = delayedL;
            hpY2L = hpY1L; hpY1L = hpL;

            float hpR = HpB0 * delayedR + HpB1 * hpX1R + HpB2 * hpX2R
                      - HpA1 * hpY1R - HpA2 * hpY2R;
            hpX2R = hpX1R; hpX1R = delayedR;
            hpY2R = hpY1R; hpY1R = hpR;

            // --- Parallel lowpass-feedback comb filters (fed with clean mid/high signal) ---
            float sumL = 0f, sumR = 0f;
            for (int c = 0; c < NumCombs; c++)
            {
                {
                    int idx = combIdxL[c];
                    float output = combBufL[c][idx];
                    combFilterL[c] = output * DampInv + combFilterL[c] * Damp;
                    combBufL[c][idx] = hpL + combFilterL[c] * Feedback;
                    combIdxL[c] = (idx + 1) % CombDelaysL[c];
                    sumL += output;
                }
                {
                    int idx = combIdxR[c];
                    float output = combBufR[c][idx];
                    combFilterR[c] = output * DampInv + combFilterR[c] * Damp;
                    combBufR[c][idx] = hpR + combFilterR[c] * Feedback;
                    combIdxR[c] = (idx + 1) % CombDelaysR[c];
                    sumR += output;
                }
            }

            sumL /= NumCombs;
            sumR /= NumCombs;

            // --- Series allpass diffusers ---
            for (int a = 0; a < NumAllpass; a++)
            {
                {
                    int idx = apIdxL[a];
                    float delayed = apBufL[a][idx];
                    apBufL[a][idx] = sumL + delayed * AllpassGain;
                    sumL = delayed - sumL * AllpassGain;
                    apIdxL[a] = (idx + 1) % AllpassDelaysL[a];
                }
                {
                    int idx = apIdxR[a];
                    float delayed = apBufR[a][idx];
                    apBufR[a][idx] = sumR + delayed * AllpassGain;
                    sumR = delayed - sumR * AllpassGain;
                    apIdxR[a] = (idx + 1) % AllpassDelaysR[a];
                }
            }

            // Dry signal unprocessed + wet reverb (bass-free, pre-delayed)
            float outL = inL * DryMix + sumL * WetMix;
            float outR = inR * DryMix + sumR * WetMix;

            samples[i] = (short)Math.Clamp(outL * 32768f, -32768f, 32767f);
            samples[i + 1] = (short)Math.Clamp(outR * 32768f, -32768f, 32767f);
        }
    }

    public void Reset()
    {
        for (int i = 0; i < NumCombs; i++)
        {
            Array.Clear(combBufL[i]);
            Array.Clear(combBufR[i]);
            combIdxL[i] = 0;
            combIdxR[i] = 0;
            combFilterL[i] = 0f;
            combFilterR[i] = 0f;
        }

        for (int i = 0; i < NumAllpass; i++)
        {
            Array.Clear(apBufL[i]);
            Array.Clear(apBufR[i]);
            apIdxL[i] = 0;
            apIdxR[i] = 0;
        }

        Array.Clear(preDelayBufL);
        Array.Clear(preDelayBufR);
        preDelayIdx = 0;

        hpX1L = hpX2L = hpY1L = hpY2L = 0f;
        hpX1R = hpX2R = hpY1R = hpY2R = 0f;
    }
}
