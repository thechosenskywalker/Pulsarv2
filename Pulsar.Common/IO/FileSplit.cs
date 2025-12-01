using Pulsar.Common.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Pulsar.Common.IO
{
    public class FileSplit : IEnumerable<FileChunk>, IDisposable
    {
        public const int MaxChunkSize = 65536;

        public string FilePath
        {
            get { return _fileStream != null ? _fileStream.Name : string.Empty; }
        }

        public long FileSize
        {
            get { return SafeLength; }
        }

        public long BytesWritten { get; private set; }

        private readonly FileStream _fileStream;
        private readonly object _ioLock = new object();
        private readonly bool _keepOpen;
        private bool _disposed;

        private long SafeLength
        {
            get
            {
                try { return _fileStream != null ? _fileStream.Length : 0; }
                catch { return 0; }
            }
        }

        public FileSplit(string filePath, FileAccess access, bool keepOpen = false)
        {
            _keepOpen = keepOpen;

            try
            {
                if (access == FileAccess.Read)
                {
                    _fileStream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        MaxChunkSize,
                        FileOptions.SequentialScan
                    );
                }
                else if (access == FileAccess.Write)
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (dir != null && dir.Length > 0 && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    _fileStream = new FileStream(
                        filePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete,
                        MaxChunkSize,
                        FileOptions.WriteThrough
                    );

                    BytesWritten = 0;
                }
                else
                {
                    throw new ArgumentException("FileAccess must be Read or Write.");
                }
            }
            catch (Exception ex)
            {
                throw new IOException("FileSplit failed to open: " + filePath, ex);
            }
        }

        // =====================================================================
        // WRITE
        // =====================================================================
        public void WriteChunk(FileChunk chunk)
        {
            if (_disposed)
                throw new ObjectDisposedException("FileSplit", "Write attempted on disposed FileSplit");

            if (chunk == null || chunk.Data == null)
                throw new ArgumentNullException("chunk");

            if (chunk.Offset < 0)
                throw new IOException("Invalid offset " + chunk.Offset);

            lock (_ioLock)
            {
                try
                {
                    // Ensure no shared buffers cause TLS tag mismatch
                    byte[] immutable = new byte[chunk.Data.Length];
                    Buffer.BlockCopy(chunk.Data, 0, immutable, 0, chunk.Data.Length);

                    _fileStream.Seek(chunk.Offset, SeekOrigin.Begin);
                    _fileStream.Write(immutable, 0, immutable.Length);

                    BytesWritten += immutable.Length;
                }
                catch (Exception ex)
                {
                    throw new IOException("FileSplit: WriteChunk failed", ex);
                }
            }
        }

        // =====================================================================
        // READ
        // =====================================================================
        public FileChunk ReadChunk(long offset)
        {
            if (_disposed)
                throw new ObjectDisposedException("FileSplit");

            lock (_ioLock)
            {
                try
                {
                    long length = SafeLength;
                    if (offset >= length)
                    {
                        return new FileChunk
                        {
                            Offset = offset,
                            Data = new byte[0]
                        };
                    }

                    long remaining = length - offset;
                    int readSize = (int)Math.Min(remaining, MaxChunkSize);

                    _fileStream.Seek(offset, SeekOrigin.Begin);

                    byte[] buffer = new byte[readSize];
                    int read = _fileStream.Read(buffer, 0, readSize);

                    if (read <= 0)
                    {
                        return new FileChunk
                        {
                            Offset = offset,
                            Data = new byte[0]
                        };
                    }

                    if (read < readSize)
                    {
                        byte[] resized = new byte[read];
                        Buffer.BlockCopy(buffer, 0, resized, 0, read);
                        buffer = resized;
                    }

                    return new FileChunk
                    {
                        Offset = offset,
                        Data = buffer
                    };
                }
                catch (Exception ex)
                {
                    throw new IOException("FileSplit: ReadChunk failed", ex);
                }
            }
        }

        // =====================================================================
        // ENUMERATOR
        // =====================================================================
        public IEnumerator<FileChunk> GetEnumerator()
        {
            long len = SafeLength;
            for (long offset = 0; offset < len; offset += MaxChunkSize)
                yield return ReadChunk(offset);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // =====================================================================
        // COMPLETE VERIFICATION
        // =====================================================================
        public bool VerifyFileComplete(long expectedSize)
        {
            if (_disposed)
                return false;

            try
            {
                Flush();
                return BytesWritten == expectedSize && SafeLength == expectedSize;
            }
            catch
            {
                return false;
            }
        }

        public void Flush()
        {
            if (_disposed)
                return;

            lock (_ioLock)
            {
                try
                {
                    _fileStream.Flush(true);
                }
                catch
                {
                    // safe ignore
                }
            }
        }

        // =====================================================================
        // DISPOSAL
        // =====================================================================
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
                return;

            _disposed = true;

            if (_keepOpen)
                return;

            lock (_ioLock)
            {
                try { _fileStream.Flush(true); } catch { }
                try { _fileStream.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
