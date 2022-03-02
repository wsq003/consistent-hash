using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ConsistentHash
{
    public class MurmurHash2
    {
        public static UInt32 Hash(Byte[] data)
        {
            return Hash(data, 0xc58f1a7b);
        }
        const UInt32 m = 0x5bd1e995;
        const Int32 r = 24;

        [StructLayout(LayoutKind.Explicit)]
        struct BytetoUInt32Converter
        {
            [FieldOffset(0)]
            public Byte[] Bytes;

            [FieldOffset(0)]
            public UInt32[] UInts;
        }

        public static UInt32 Hash(Byte[] data, UInt32 seed)
        {
            Int32 length = data.Length;
            if (length == 0)
                return 0;
            UInt32 h = seed ^ (UInt32)length;
            Int32 currentIndex = 0;
            // array will be length of Bytes but contains Uints
            // therefore the currentIndex will jump with +1 while length will jump with +4
            UInt32[] hackArray = new BytetoUInt32Converter { Bytes = data }.UInts;
            while (length >= 4)
            {
                UInt32 k = hackArray[currentIndex++];
                k *= m;
                k ^= k >> r;
                k *= m;

                h *= m;
                h ^= k;
                length -= 4;
            }
            currentIndex *= 4; // fix the length
            switch (length)
            {
                case 3:
                    h ^= (UInt16)(data[currentIndex++] | data[currentIndex++] << 8);
                    h ^= (UInt32)data[currentIndex] << 16;
                    h *= m;
                    break;
                case 2:
                    h ^= (UInt16)(data[currentIndex++] | data[currentIndex] << 8);
                    h *= m;
                    break;
                case 1:
                    h ^= data[currentIndex];
                    h *= m;
                    break;
                default:
                    break;
            }

            // Do a few final mixes of the hash to ensure the last few
            // bytes are well-incorporated.

            h ^= h >> 13;
            h *= m;
            h ^= h >> 15;

            return h;
        }
    }

    class ConsistentHash<T>
    {
        SortedDictionary<int, T> circle = new SortedDictionary<int, T>();
        int _replicate = 100;    //default _replicate count
        int[] ayKeys = null;    //cache the ordered keys for better performance

        //it's better you override the GetHashCode() of T.
        //we will use GetHashCode() to identify different node.
        public void Init(IEnumerable<T> nodes)
        {
            Init(nodes, _replicate);
        }

        public void Init(IEnumerable<T> nodes, int replicate)
        {
            _replicate = replicate;

            foreach (T node in nodes)
            {
                this.Add(node, false);
            }
            ayKeys = circle.Keys.ToArray();
        }

        public void Add(T node)
        {
            Add(node, true);
        }

        private void Add(T node, bool updateKeyArray)
        {
            for (int i = 0; i < _replicate; i++)
            {
                int hash = BetterHash(node.GetHashCode().ToString() + i);
                circle[hash] = node;
            }

            if (updateKeyArray)
            {
                ayKeys = circle.Keys.ToArray();
            }
        }

        public void Remove(T node)
        {
            for (int i = 0; i < _replicate; i++)
            {
                int hash = BetterHash(node.GetHashCode().ToString() + i);
                if (!circle.Remove(hash))
                {
                    //throw new Exception("can not remove a node that not added");
                }
            }
            ayKeys = circle.Keys.ToArray();
        }

        //we keep this function just for performance compare
        private T GetNode_slow(String key)
        {
            int hash = BetterHash(key);
            if (circle.ContainsKey(hash))
            {
                return circle[hash];
            }

            int first = circle.Keys.FirstOrDefault(h => h >= hash);
            if (first == new int())
            {
                first = ayKeys[0];
            }
            T node = circle[first];
            return node;
        }

        //return the index of first item that >= val.
        //if not exist, return 0;
        //ay should be ordered array.
        int First_ge(int[] ay, int val)
        {
            int begin = 0;
            int end = ay.Length - 1;

            if (ay[end] < val || ay[0] > val)
            {
                return 0;
            }

            int mid = begin;
            while (end - begin > 1)
            {
                mid = (end + begin) / 2;
                if (ay[mid] >= val)
                {
                    end = mid;
                }
                else
                {
                    begin = mid;
                }
            }

            if (ay[begin] > val || ay[end] < val)
            {
                throw new Exception("should not happen");
            }

            return end;
        }

        public T GetNode(String key)
        {
            //return GetNode_slow(key);

            int hash = BetterHash(key);

            int first = First_ge(ayKeys, hash);

            //int diff = circle.Keys[first] - hash;

            return circle[ayKeys[first]];
        }

        //default String.GetHashCode() can't well spread strings like "1", "2", "3"
        public static int BetterHash(String key)
        {
            uint hash = MurmurHash2.Hash(Encoding.ASCII.GetBytes(key));
            return (int)hash;
        }
    }
}




