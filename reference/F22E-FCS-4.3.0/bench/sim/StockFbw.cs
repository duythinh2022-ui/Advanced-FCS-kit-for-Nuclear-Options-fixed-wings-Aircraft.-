using System;
using UnityEngine;
// Port of NuclearOption ControlsFilter.FlyByWire.Filter with Aryx's F-22E parameters (flight path only; no gear/ground logic).
internal sealed class StockFbw
{
    float gLimitPositive = 9.5f, cornerSpeed = 140f, maxRollSpeed = 210f, maxPitchAngularVel = 1.25f, maxRollAngularVel = 10f;
    float alphaLimiter = 31f, alphaLimiterStrength = 0.05f, pFactorFast = 7f, iFactor = 0.01f, dFactorFast = 1.22f;
    float rollTrimRate = 0.05f, rollTrimLimit = 1f, yawTightness = 2f, rollTightness = 1.1f;
    float pPrev, i, pitchAdjuster, rollTrim, limitFactorSmoothed;
    public void Filter(float speed, float rho, Vector3 velWorld, Quaternion rot, Vector3 wWorld, ControlInputs inputs, float dt)
    {
        Vector3 lav = Quaternion.Inverse(rot) * wWorld;
        float num = cornerSpeed * cornerSpeed * 1.225f;
        float num2 = speed * speed * rho / num;
        float remapFactor = 1f / Mathf.Max(num2, 1f);
        limitFactorSmoothed = Mathf.Lerp(limitFactorSmoothed, 1f, dt);   // stabilityAssist = true
        float target = inputs.pitch * gLimitPositive * 9.81f / Mathf.Max(speed, cornerSpeed * 0.75f);
        if (num2 < 1f)
        {
            target *= Mathf.Clamp(num2, 0.3f, 1f);
            Vector3 v = Quaternion.Inverse(rot) * velWorld;
            float f = Mathf.Atan2(v.y, v.z) * 57.29578f;
            if (Mathf.Abs(f) > alphaLimiter && Mathf.Sign(f) == Mathf.Sign(target))
                target *= 1f - Mathf.Clamp(Mathf.Abs(f) - alphaLimiter, 0f, 10f) * alphaLimiterStrength;
        }
        target = Mathf.Lerp(inputs.pitch * maxPitchAngularVel, target, limitFactorSmoothed);
        float num4 = Mathf.Clamp(lav.x - target, -0.25f, 0.25f);
        i = Mathf.Clamp(i + num4 * dt, -0.2f, 0.2f);
        float num5 = (num4 - pPrev) / dt; pPrev = num4;
        float value2 = (0f - (num4 * pFactorFast + i * iFactor + num5 * dFactorFast)) * remapFactor;
        pitchAdjuster += Mathf.Clamp(value2, -2f, 2f) * dt;
        pitchAdjuster = Mathf.Clamp(pitchAdjuster, -1f, 1f);
        inputs.pitch = pitchAdjuster;
        float num6 = yawTightness * remapFactor * (lav.y - inputs.yaw);
        inputs.yaw = Mathf.Clamp(0f - num6, -1f, 1f);
        float num7 = lav.z - inputs.roll * (0f - maxRollAngularVel) * Mathf.Clamp(num2 / maxRollSpeed, 0.5f, 1f);
        if (Mathf.Abs(lav.x) < 0.1f)
            rollTrim += Mathf.Clamp(Mathf.Clamp(num7, -0.1f, 0.1f), (0f - rollTrimRate) * dt, rollTrimRate * dt);
        rollTrim = Mathf.Clamp(rollTrim, 0f - rollTrimLimit, rollTrimLimit);
        if (num2 > maxRollSpeed) inputs.roll *= maxRollSpeed * maxRollSpeed / (num2 * num2);
        inputs.roll = Mathf.Lerp(inputs.roll, num7 * rollTightness, remapFactor);
        inputs.roll = Mathf.Clamp(inputs.roll + rollTrim * remapFactor, -1f, 1f);
    }
}
