using Domain.Enumerations;
using Domain.ValueObjects;
using System.Security.Cryptography;

namespace Domain.Services
{
    public sealed class MultiAlgorithmChecksumService : IChecksumService
    {
        public Checksum Compute(Stream source, ChecksumAlgorithm algorithm)
        {
            source.Position = 0;

            var hash = algorithm switch
            {
                ChecksumAlgorithm.Sha256 => ComputeSha256(source),
                ChecksumAlgorithm.Sha512 => ComputeSha512(source),
                ChecksumAlgorithm.Shake256 => ComputeShake256(source),
                ChecksumAlgorithm.Blake3 => ComputeBlake3(source),
                _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported.")
            };

            return new Checksum(hash, algorithm);
        }

        public bool Verify(Stream source, Checksum checksum) =>
            Compute(source, checksum.algorithm).value.SequenceEqual(checksum.value);

        private static byte[] ComputeSha256(Stream source)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(source);
        }

        private static byte[] ComputeSha512(Stream source)
        {
            using var sha512 = SHA512.Create();
            return sha512.ComputeHash(source);
        }

        private static byte[] ComputeShake256(Stream source)
        {
            var buffer = new byte[4096];
            var output = new byte[32];
            
            using var shake = new Shake256Implementation();
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                shake.AppendData(buffer.AsSpan(0, bytesRead));
            }
            
            shake.GetHashAndReset(output);
            return output;
        }

        private static byte[] ComputeBlake3(Stream source)
        {
            using var blake3 = new Blake3Implementation();
            var buffer = new byte[4096];
            int bytesRead;
            
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                blake3.Update(buffer.AsSpan(0, bytesRead));
            }
            
            return blake3.Finalize();
        }
    }

    internal sealed class Shake256Implementation : IDisposable
    {
        private const int Rate = 136; 
        private const int Capacity = 64;
        private const int StateSize = 200; 

        private readonly byte[] _state = new byte[StateSize];
        private readonly List<byte> _buffer = new();
        private bool _finalized = false;

        public void AppendData(ReadOnlySpan<byte> data)
        {
            if (_finalized)
                throw new InvalidOperationException("Cannot append data after finalization");

            _buffer.AddRange(data.ToArray());
        }

        public void GetHashAndReset(Span<byte> output)
        {
            if (!_finalized)
            {
                FinalizeShake();
            }

            Squeeze(output);
            Reset();
        }

        private void FinalizeShake()
        {
            var paddedMessage = PadMessage(_buffer.ToArray());
            
            for (int i = 0; i < paddedMessage.Length; i += Rate)
            {
                var blockSize = Math.Min(Rate, paddedMessage.Length - i);
                var block = paddedMessage.AsSpan(i, blockSize);
                
                for (int j = 0; j < blockSize; j++)
                {
                    _state[j] ^= block[j];
                }
                
                KeccakF();
            }

            _finalized = true;
        }

        private byte[] PadMessage(byte[] message)
        {
            var paddedLength = ((message.Length + 1 + Rate - 1) / Rate) * Rate;
            var padded = new byte[paddedLength];
            
            Array.Copy(message, 0, padded, 0, message.Length);
            padded[message.Length] = 0x1F; 
            padded[paddedLength - 1] |= 0x80; 
            
            return padded;
        }

        private void Squeeze(Span<byte> output)
        {
            int outputOffset = 0;
            
            while (outputOffset < output.Length)
            {
                var bytesToCopy = Math.Min(Rate, output.Length - outputOffset);
                _state.AsSpan(0, bytesToCopy).CopyTo(output.Slice(outputOffset));
                outputOffset += bytesToCopy;
                
                if (outputOffset < output.Length)
                {
                    KeccakF();
                }
            }
        }

        private void KeccakF()
        {
            var lanes = new ulong[25];
            
            for (int i = 0; i < 25; i++)
            {
                lanes[i] = BitConverter.ToUInt64(_state, i * 8);
            }

            for (int round = 0; round < 24; round++)
            {
                var C = new ulong[5];
                for (int x = 0; x < 5; x++)
                {
                    C[x] = lanes[x] ^ lanes[x + 5] ^ lanes[x + 10] ^ lanes[x + 15] ^ lanes[x + 20];
                }

                var D = new ulong[5];
                for (int x = 0; x < 5; x++)
                {
                    D[x] = C[(x + 4) % 5] ^ RotateLeft(C[(x + 1) % 5], 1);
                }

                for (int x = 0; x < 5; x++)
                {
                    for (int y = 0; y < 5; y++)
                    {
                        lanes[y * 5 + x] ^= D[x];
                    }
                }

                var current = lanes[1];
                for (int t = 0; t < 24; t++)
                {
                    var next = ((t + 1) * (t + 2) / 2) % 25;
                    var temp = lanes[next];
                    lanes[next] = RotateLeft(current, GetRotationOffset(t));
                    current = temp;
                }

                for (int y = 0; y < 5; y++)
                {
                    var temp = new ulong[5];
                    for (int x = 0; x < 5; x++)
                    {
                        temp[x] = lanes[y * 5 + x] ^ ((~lanes[y * 5 + (x + 1) % 5]) & lanes[y * 5 + (x + 2) % 5]);
                    }
                    for (int x = 0; x < 5; x++)
                    {
                        lanes[y * 5 + x] = temp[x];
                    }
                }

                lanes[0] ^= GetRoundConstant(round);
            }

            for (int i = 0; i < 25; i++)
            {
                var bytes = BitConverter.GetBytes(lanes[i]);
                Array.Copy(bytes, 0, _state, i * 8, 8);
            }
        }

        private static ulong RotateLeft(ulong value, int offset)
        {
            return (value << offset) | (value >> (64 - offset));
        }

        private static int GetRotationOffset(int t)
        {
            var offsets = new int[] { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 2, 14, 27, 41, 56, 8, 25, 43, 62, 18, 39, 61, 20, 44 };
            return offsets[t];
        }

        private static ulong GetRoundConstant(int round)
        {
            var constants = new ulong[]
            {
                0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL,
                0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
                0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL,
                0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL,
                0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL,
                0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL
            };
            return constants[round];
        }

        private void Reset()
        {
            Array.Clear(_state);
            _buffer.Clear();
            _finalized = false;
        }

        public void Dispose()
        {
            Array.Clear(_state);
            _buffer.Clear();
        }
    }

    internal sealed class Blake3Implementation : IDisposable
    {
        private const int BlockSize = 64;
        private const int ChunkSize = 1024;
        private const int OutLen = 32;

        private static readonly uint[] IV = {
            0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
            0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19
        };

        private readonly List<byte[]> _chunks = new();
        private readonly List<byte> _currentChunk = new();
        private readonly byte[] _key = new byte[32];

        public Blake3Implementation()
        {
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(_key);
        }

        public void Update(ReadOnlySpan<byte> data)
        {
            var dataArray = data.ToArray();
            _currentChunk.AddRange(dataArray);

            while (_currentChunk.Count >= ChunkSize)
            {
                var chunkData = _currentChunk.Take(ChunkSize).ToArray();
                _chunks.Add(chunkData);
                _currentChunk.RemoveRange(0, ChunkSize);
            }
        }

        public byte[] Finalize()
        {
            if (_currentChunk.Count > 0)
            {
                _chunks.Add(_currentChunk.ToArray());
            }

            if (_chunks.Count == 0)
            {
                return HashChunk(Array.Empty<byte>(), 0, true);
            }

            if (_chunks.Count == 1)
            {
                return HashChunk(_chunks[0], 0, true);
            }

            return BuildMerkleTree(_chunks);
        }

        private byte[] HashChunk(byte[] chunk, ulong chunkCounter, bool isRoot)
        {
            var state = new uint[8];
            Array.Copy(IV, state, 8);

            for (int i = 0; i < chunk.Length; i += BlockSize)
            {
                var blockSize = Math.Min(BlockSize, chunk.Length - i);
                var block = new byte[BlockSize];
                Array.Copy(chunk, i, block, 0, blockSize);
                
                var isLastBlock = (i + BlockSize >= chunk.Length);
                CompressBlock(state, block, chunkCounter, isLastBlock, isRoot);
            }

            var output = new byte[OutLen];
            for (int i = 0; i < 8; i++)
            {
                var bytes = BitConverter.GetBytes(state[i]);
                Array.Copy(bytes, 0, output, i * 4, 4);
            }

            return output;
        }

        private void CompressBlock(uint[] state, byte[] block, ulong chunkCounter, bool isLastBlock, bool isRoot)
        {
            var m = new uint[16];
            for (int i = 0; i < 16; i++)
            {
                m[i] = BitConverter.ToUInt32(block, i * 4);
            }

            var v = new uint[16];
            Array.Copy(state, v, 8);
            Array.Copy(IV, 0, v, 8, 8);

            v[12] = (uint)(chunkCounter & 0xFFFFFFFF);
            v[13] = (uint)(chunkCounter >> 32);
            if (isLastBlock) v[14] |= 1;
            if (isRoot) v[15] |= 2;

            for (int round = 0; round < 7; round++)
            {
                G(v, 0, 4, 8, 12, m[GetMessageWord(round, 0)], m[GetMessageWord(round, 1)]);
                G(v, 1, 5, 9, 13, m[GetMessageWord(round, 2)], m[GetMessageWord(round, 3)]);
                G(v, 2, 6, 10, 14, m[GetMessageWord(round, 4)], m[GetMessageWord(round, 5)]);
                G(v, 3, 7, 11, 15, m[GetMessageWord(round, 6)], m[GetMessageWord(round, 7)]);

                G(v, 0, 5, 10, 15, m[GetMessageWord(round, 8)], m[GetMessageWord(round, 9)]);
                G(v, 1, 6, 11, 12, m[GetMessageWord(round, 10)], m[GetMessageWord(round, 11)]);
                G(v, 2, 7, 8, 13, m[GetMessageWord(round, 12)], m[GetMessageWord(round, 13)]);
                G(v, 3, 4, 9, 14, m[GetMessageWord(round, 14)], m[GetMessageWord(round, 15)]);
            }

            for (int i = 0; i < 8; i++)
            {
                state[i] ^= v[i] ^ v[i + 8];
            }
        }

        private static void G(uint[] v, int a, int b, int c, int d, uint x, uint y)
        {
            v[a] = v[a] + v[b] + x;
            v[d] = RotateRight(v[d] ^ v[a], 16);
            v[c] = v[c] + v[d];
            v[b] = RotateRight(v[b] ^ v[c], 12);
            v[a] = v[a] + v[b] + y;
            v[d] = RotateRight(v[d] ^ v[a], 8);
            v[c] = v[c] + v[d];
            v[b] = RotateRight(v[b] ^ v[c], 7);
        }

        private static uint RotateRight(uint value, int offset)
        {
            return (value >> offset) | (value << (32 - offset));
        }

        private static int GetMessageWord(int round, int position)
        {
            var schedule = new int[,] {
                {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15},
                {2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8},
                {3, 4, 10, 12, 13, 2, 7, 14, 6, 5, 9, 0, 11, 15, 8, 1},
                {10, 7, 12, 9, 14, 3, 13, 15, 4, 0, 11, 2, 5, 8, 1, 6},
                {12, 13, 9, 11, 15, 10, 14, 8, 7, 2, 5, 3, 0, 1, 6, 4},
                {9, 14, 11, 5, 8, 12, 15, 1, 13, 3, 0, 10, 2, 6, 4, 7},
                {11, 15, 5, 0, 1, 9, 8, 6, 14, 10, 2, 12, 3, 4, 7, 13}
            };
            return schedule[round, position];
        }

        private byte[] BuildMerkleTree(List<byte[]> chunks)
        {
            var level = new List<byte[]>();
            
            for (int i = 0; i < chunks.Count; i++)
            {
                level.Add(HashChunk(chunks[i], (ulong)i, false));
            }

            while (level.Count > 1)
            {
                var nextLevel = new List<byte[]>();
                
                for (int i = 0; i < level.Count; i += 2)
                {
                    if (i + 1 < level.Count)
                    {
                        var combined = new byte[level[i].Length + level[i + 1].Length];
                        Array.Copy(level[i], 0, combined, 0, level[i].Length);
                        Array.Copy(level[i + 1], 0, combined, level[i].Length, level[i + 1].Length);
                        nextLevel.Add(HashChunk(combined, 0, i + 2 >= level.Count));
                    }
                    else
                    {
                        nextLevel.Add(level[i]);
                    }
                }
                
                level = nextLevel;
            }

            return level[0];
        }

        public void Dispose()
        {
            _chunks.Clear();
            _currentChunk.Clear();
            Array.Clear(_key);
        }
    }
}
