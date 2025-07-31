using System;
using System.Runtime.InteropServices;
using Bit;
using UnityEngine;

public class Test : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            // 半精度 圧縮/復元テスト
            
            // var rotation = transform.rotation;
            
            // TODO: 精度限界については後で調べる。たぶん計算式がありそう
            // var rotation = new Vector4(0.0001f, 10000f, 0, 0); // 16bitのときの限界値 指数オフセット-15
            var rotation = new Vector4(0.000000001f, 1.5f, 0, 0); // 16bit 指数オフセット-31

            Debug.Log($"before: {rotation}");
            var temp = rotation;

            Bit16 bx = ToBit16(rotation.x);
            Bit16 by = ToBit16(rotation.y);
            Bit16 bz = ToBit16(rotation.z);
            Bit16 bw = ToBit16(rotation.w);

            rotation.x = ToFloat(bx);
            rotation.y = ToFloat(by);
            rotation.z = ToFloat(bz);
            rotation.w = ToFloat(bw);

            Debug.Log($"after: {rotation}");

            var diffX = MathF.Abs(temp.x - rotation.x);
            var diffY = MathF.Abs(temp.y - rotation.y);
            var diffZ = MathF.Abs(temp.z - rotation.z);
            Debug.Log($"diff x: {diffX}, y: {diffY}, z: {diffZ}");
            
            // transform.rotation = rotation;
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            // Quaternion wの圧縮/復元テスト
            
            var rotateAxis = new Vector3(0, 0, 1).normalized;

            var degrees = 90f;
            var rad = degrees * Mathf.Deg2Rad;

            Quaternion q;
            q.x = rotateAxis.x * Mathf.Sin(rad * 0.5f);
            q.y = rotateAxis.y * Mathf.Sin(rad * 0.5f);
            q.z = rotateAxis.z * Mathf.Sin(rad * 0.5f);
            q.w = Mathf.Sqrt(1 - (q.x * q.x + q.y * q.y + q.z * q.z));

            Debug.Log(q);

            var trans = transform;
            trans.rotation = q * trans.rotation;

            if (q.w < 0)
            {
                // 回転軸ベクトルが単位ベクトルの場合、wが負になることはなさそう
                Debug.LogError($"minus: {degrees}");
            }
        }
    }

    static Vector3 EncodeBasicHalfAngle(Quaternion q)
    {
        if (q.w < 0)
        {
            // 回転軸ベクトルが単位ベクトルの場合、wが負になることはなさそうなのでここは通らないはず
            q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
        }

        return new Vector3(q.x, q.y, q.z) * 1 / Mathf.Sqrt(1f + q.w);
    }

    static Quaternion DecodeBasicHalfAngle(in Vector3 p)
    {
        var d = Vector3.Dot(p, p);
        var s = Mathf.Sqrt(2f - d);
        return new Quaternion(s * p.x, s * p.y, s * p.z, 1f - d);
    }


    [StructLayout(LayoutKind.Explicit)]
    private struct FloatConverter
    {
        [FieldOffset(0)] public float f;
        [FieldOffset(0)] public uint n;
    }

    static Bit16 ToBit16(float f)
    {
        // c#の浮動小数点はIEEE754形式 (→ 浮動小数点型項目参照 https://learn.microsoft.com/ja-jp/dotnet/csharp/language-reference/language-specification/types#837-floating-point-types)
        // 符号(sign): 1bit (31bit目)
        // 指数部(exponent): 8bit (23~30bit目)
        // 仮数部(fraction): 23bit (0~22bit目)
        // |------------+---------+-------------|
        // | 種類       | 指数部  | 仮数部      |
        // |============+=========+=============|
        // | ゼロ       | 0       | 0           |
        // |------------+---------+-------------|
        // | 非正規化数 | 0       | 0以外       |
        // |------------+---------+-------------|
        // | 正規仮数   | 1 - 254 | 任意        |
        // |------------+---------+-------------|
        // | 無限大     | 255     | 0           |
        // |------------+---------+-------------|
        // | NaN        | 255     | 0以外の任意 |
        // |------------+---------+-------------|
        // 参照 (https://ja.wikipedia.org/wiki/IEEE_754)

        FloatConverter c = new FloatConverter { f = f };
        uint n = c.n;

        // Debug.Log($"old n: {Convert.ToString(c.n, 2).PadLeft(32, '0')}");

        // 符号
        uint sign_bit = (n >> 16) & 0x8000;

        // 指数部
        bool isZero_exponent = ((n >> 23) & 0xff) == 0; // 0xff = 1111 1111
        // 指数部は+127でバイアスされているので打ち消す
        // 新たな指数部を5bit(32 - 1(符号) - 10(仮数部) = 5)とし、
        // -31 (最大値が0となるように。1.0以下の数値しか扱わないことから、可能な限り精度を上げるため) でバイアスしておく
        uint exponent = (((n >> 23) - 127 + 31) & 0x1f) << 10; // 0x1f = 0001 1111
        // uint exponent = (((n >> 23) - 127 + 15) & 0x1f) << 10; // IEEE754の半精度はバイアス-15

        // 仮数部
        // 新たな仮数部を10bitとする
        uint fraction = (n >> (23 - 10)) & 0x3ff; // 0x3ff = 0011 1111 1111
        bool isZero_fraction = fraction == 0;

        // 指数部、仮数部ともに0の場合は0
        var res = isZero_exponent && isZero_fraction
            ? new Bit16(0)
            : new Bit16((ushort)(sign_bit | exponent | fraction));

        return res;
    }

    static float ToFloat(Bit16 b)
    {
        uint sign_bit = ((uint)b.Value & 0x8000) << 16;

        // uint exponent = (((((uint)b.Value >> 10) & 0x1f) - 15 + 127) & 0xff) << 23;
        uint exponent = (((((uint)b.Value >> 10) & 0x1f) - 31 + 127) & 0xff) << 23;

        uint fraction = ((uint)b.Value & 0x3ff) << (23 - 10);

        uint res = sign_bit | exponent | fraction;

        FloatConverter c = new FloatConverter { n = res };
        // Debug.Log($"new n: {Convert.ToString(c.n, 2).PadLeft(32, '0')}");
        return c.f;
    }

    // 参考 https://stackoverflow.com/questions/268853/is-it-possible-to-write-quakes-fast-invsqrt-function-in-c
    // 今はもう普通に計算したほうが早いらしい
    // static float FastInvSqrt(float x)
    // {
    //     float xhalf = 0.5f * x;
    //     // int i = BitConverter.SingleToInt32Bits(x);
    //     int i = BitConverter.ToInt32(BitConverter.GetBytes(x), 0);
    //     i = 0x5F375A86 - (i >> 1);
    //     // x = BitConverter.Int32BitsToSingle(i);
    //     x = BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
    //     x = x * (1.5f - xhalf * x * x);
    //     return x;
    // }
}