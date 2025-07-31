using System;

namespace Bit
{
    public struct Bit16 : IEquatable<Bit16>
    {
        public ushort Value;
        public Bit16(ushort x) => Value = x;

        public bool Equals(Bit16 other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Bit16 other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(Bit16 x, Bit16 y) => x.Value == y.Value;
        public static bool operator !=(Bit16 x, Bit16 y) => x.Value != y.Value;

        public static implicit operator Bit16(ushort x) => new Bit16(x);
        public static implicit operator ushort(Bit16 x) => x.Value;

        public override string ToString() => Value.ToString();
    }
}