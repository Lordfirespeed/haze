using System;
using System.Collections.Generic;
using System.Linq;
using SteamKit2;

namespace Haze.Steam;

public static class KeyValueExtensions
{
    extension(KeyValue keyValue)
    {
        public IEnumerable<T> AsArray<T>(Func<KeyValue, T> mapper)
        {
            return keyValue.Children.Select(mapper);
        }

        public IEnumerable<byte> AsUnsignedByteArray()
        {
            return keyValue.AsArray(child => child.AsUnsignedByte());
        }

        public IEnumerable<ushort> AsUnsignedShortArray()
        {
            return keyValue.AsArray(child => child.AsUnsignedShort());
        }

        public IEnumerable<uint> AsUnsignedIntegerArray()
        {
            return keyValue.AsArray(child => child.AsUnsignedInteger());
        }

        public IEnumerable<ulong> AsUnsignedLongArray()
        {
            return keyValue.AsArray(child => child.AsUnsignedLong());
        }

        // KeyValue.AsShort is missing, perhaps for good reason, so is skipped

        public IEnumerable<int> AsIntegerArray()
        {
            return keyValue.AsArray(child => child.AsInteger());
        }

        public IEnumerable<long> AsLongArray()
        {
            return keyValue.AsArray(child => child.AsLong());
        }

        public IEnumerable<float> AsFloatArray()
        {
            return keyValue.AsArray(child => child.AsFloat());
        }

        public IEnumerable<bool> AsBooleanArray()
        {
            return keyValue.AsArray(child => child.AsBoolean());
        }

        public IEnumerable<TEnum> AsEnumArray<TEnum>(TEnum defaultValue = default) where TEnum: struct
        {
            return keyValue.AsArray(child => child.AsEnum(defaultValue));
        }
    }

}
