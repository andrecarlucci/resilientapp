﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO {
    // A MemoryStream represents a Stream in memory (ie, it has no backing store).
    // This stream may reduce the need for temporary buffers and files in 
    // an application.  
    // 
    // There are two ways to create a MemoryStream.  You can initialize one
    // from an unsigned byte array, or you can create an empty one.  Empty 
    // memory streams are resizable, while ones created with a byte array provide
    // a stream "view" of the data.
    public class FakeMemoryStream : Stream {
        private byte[] _buffer;    // Either allocated internally or externally.
        private int _origin;       // For user-provided arrays, start at this origin
        private int _position;     // read/write head.
        private int _length;       // Number of bytes within the memory stream
        private int _capacity;     // length of usable portion of buffer for stream
        // Note that _capacity == _buffer.Length for non-user-provided byte[]'s

        private bool _expandable;  // User-provided buffers aren't expandable.
        private bool _writable;    // Can user write to this stream?
        private bool _exposable;   // Whether the array can be returned to the user.
        private bool _isOpen;      // Is this stream open or closed?

        private Task<int> _lastReadTask; // The last successful task returned from ReadAsync

        private const int MemStreamMaxLength = int.MaxValue;

        public FakeMemoryStream()
            : this(0) {
        }

        public FakeMemoryStream(int capacity) {
            _buffer = capacity != 0 ? new byte[capacity] : Array.Empty<byte>();
            _capacity = capacity;
            _expandable = true;
            _writable = true;
            _exposable = true;
            _origin = 0;      // Must be 0 for byte[]'s created by MemoryStream
            _isOpen = true;
        }

        public FakeMemoryStream(byte[] buffer)
            : this(buffer, true) {
        }

        public FakeMemoryStream(byte[] buffer, bool writable) {

            _buffer = buffer;
            _length = _capacity = buffer.Length;
            _writable = writable;
            _exposable = false;
            _origin = 0;
            _isOpen = true;
        }

        public FakeMemoryStream(byte[] buffer, int index, int count)
            : this(buffer, index, count, true, false) {
        }

        public FakeMemoryStream(byte[] buffer, int index, int count, bool writable)
            : this(buffer, index, count, writable, false) {
        }

        public FakeMemoryStream(byte[] buffer, int index, int count, bool writable, bool publiclyVisible) {
            _buffer = buffer;
            _origin = _position = index;
            _length = _capacity = index + count;
            _writable = writable;
            _exposable = publiclyVisible;  // Can TryGetBuffer/GetBuffer return the array?
            _expandable = false;
            _isOpen = true;
        }

        public override bool CanRead => _isOpen;

        public override bool CanSeek => _isOpen;

        public override bool CanWrite => _writable;

        private void EnsureNotClosed() {
        }

        private void EnsureWriteable() {
        }

        protected override void Dispose(bool disposing) {
            //try {
            //    if (disposing) {
            //        _isOpen = false;
            //        _writable = false;
            //        _expandable = false;
            //        // Don't set buffer to null - allow TryGetBuffer, GetBuffer & ToArray to work.
            //        _lastReadTask = null;
            //    }
            //}
            //finally {
            //    // Call base.Close() to cleanup async IO resources
            //    base.Dispose(disposing);
            //}
        }

        // returns a bool saying whether we allocated a new array.
        private bool EnsureCapacity(int value) {
            // Check for overflow

            if (value > _capacity) {
                int newCapacity = value;
                if (newCapacity < 256) {
                    newCapacity = 256;
                }

                // We are ok with this overflowing since the next statement will deal
                // with the cases where _capacity*2 overflows.
                if (newCapacity < _capacity * 2) {
                    newCapacity = _capacity * 2;
                }

                // We want to expand the array up to Array.MaxByteArrayLength
                // And we want to give the user the value that they asked for
                if ((uint)(_capacity * 2) > int.MaxValue) {
                    newCapacity = value > int.MaxValue ? value : int.MaxValue;
                }

                Capacity = newCapacity;
                return true;
            }
            return false;
        }

        public override void Flush() {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            try {
                Flush();
                return Task.CompletedTask;
            }
            catch (Exception ex) {
                return Task.FromException(ex);
            }
        }


        public virtual byte[] GetBuffer() {
            return _buffer;
        }

        public virtual bool TryGetBuffer(out ArraySegment<byte> buffer) {
            if (!_exposable) {
                buffer = default(ArraySegment<byte>);
                return false;
            }

            buffer = new ArraySegment<byte>(_buffer, offset: _origin, count: (_length - _origin));
            return true;
        }

        // -------------- PERF: Internal functions for fast direct access of MemoryStream buffer (cf. BinaryReader for usage) ---------------

        // PERF: Internal sibling of GetBuffer, always returns a buffer (cf. GetBuffer())
        internal byte[] InternalGetBuffer() {
            return _buffer;
        }

        // PERF: True cursor position, we don't need _origin for direct access
        internal int InternalGetPosition() {
            return _position;
        }

        // PERF: Takes out Int32 as fast as possible
        internal int InternalReadInt32() {
            EnsureNotClosed();

            int pos = (_position += 4); // use temp to avoid a race condition
            if (pos > _length) {
                _position = _length;
                throw new Exception();
            }
            return (int)(_buffer[pos - 4] | _buffer[pos - 3] << 8 | _buffer[pos - 2] << 16 | _buffer[pos - 1] << 24);
        }

        // PERF: Get actual length of bytes available for read; do sanity checks; shift position - i.e. everything except actual copying bytes
        internal int InternalEmulateRead(int count) {
            EnsureNotClosed();

            int n = _length - _position;
            if (n > count)
                n = count;
            if (n < 0)
                n = 0;

            Debug.Assert(_position + n >= 0, "_position + n >= 0");  // len is less than 2^31 -1.
            _position += n;
            return n;
        }

        // Gets & sets the capacity (number of bytes allocated) for this stream.
        // The capacity cannot be set to a value less than the current length
        // of the stream.
        // 
        public virtual int Capacity {
            get {
                EnsureNotClosed();
                return _capacity - _origin;
            }
            set {
                // Only update the capacity if the MS is expandable and the value is different than the current capacity.
                // Special behavior if the MS isn't expandable: we don't throw if value is the same as the current capacity
                
                EnsureNotClosed();


                // MemoryStream has this invariant: _origin > 0 => !expandable (see ctors)
                if (_expandable && value != _capacity) {
                    if (value > 0) {
                        byte[] newBuffer = new byte[value];
                        if (_length > 0) {
                            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);
                        }
                        _buffer = newBuffer;
                    }
                    else {
                        _buffer = null;
                    }
                    _capacity = value;
                }
            }
        }

        public override long Length {
            get {
                EnsureNotClosed();
                return _length - _origin;
            }
        }

        public override long Position {
            get {
                EnsureNotClosed();
                return _position - _origin;
            }
            set {

                EnsureNotClosed();

                _position = _origin + (int)value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            
            EnsureNotClosed();

            int n = _length - _position;
            if (n > count)
                n = count;
            if (n <= 0)
                return 0;

            Debug.Assert(_position + n >= 0, "_position + n >= 0");  // len is less than 2^31 -1.

            if (n <= 8) {
                int byteCount = n;
                while (--byteCount >= 0)
                    buffer[offset + byteCount] = _buffer[_position + byteCount];
            }
            else
                Buffer.BlockCopy(_buffer, _position, buffer, offset, n);
            _position += n;

            return n;
        }

        public override int Read(Span<byte> buffer) {
            if (GetType() != typeof(MemoryStream)) {
                // MemoryStream is not sealed, and a derived type may have overridden Read(byte[], int, int) prior
                // to this Read(Span<byte>) overload being introduced.  In that case, this Read(Span<byte>) overload
                // should use the behavior of Read(byte[],int,int) overload.
                return base.Read(buffer);
            }

            EnsureNotClosed();

            int n = Math.Min(_length - _position, buffer.Length);
            if (n <= 0)
                return 0;

            // TODO https://github.com/dotnet/coreclr/issues/15076:
            // Read(byte[], int, int) has an n <= 8 optimization, presumably based
            // on benchmarking.  Determine if/where such a cut-off is here and add
            // an equivalent optimization if necessary.
            new Span<byte>(_buffer, _position, n).CopyTo(buffer);

            _position += n;
            return n;
        }

        public override Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken) {

            // If cancellation was requested, bail early
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<int>(cancellationToken);

            try {
                int n = Read(buffer, offset, count);
                var t = _lastReadTask;
                Debug.Assert(t == null || t.Status == TaskStatus.RanToCompletion,
                    "Expected that a stored last task completed successfully");
                return (t != null && t.Result == n) ? t : (_lastReadTask = Task.FromResult<int>(n));
            }
            
            catch (Exception exception) {
                return Task.FromException<int>(exception);
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken)) {
            if (cancellationToken.IsCancellationRequested) {
                return new ValueTask<int>(Task.FromCanceled<int>(cancellationToken));
            }

            try {
                // ReadAsync(Memory<byte>,...) needs to delegate to an existing virtual to do the work, in case an existing derived type
                // has changed or augmented the logic associated with reads.  If the Memory wraps an array, we could delegate to
                // ReadAsync(byte[], ...), but that would defeat part of the purpose, as ReadAsync(byte[], ...) often needs to allocate
                // a Task<int> for the return value, so we want to delegate to one of the synchronous methods.  We could always
                // delegate to the Read(Span<byte>) method, and that's the most efficient solution when dealing with a concrete
                // MemoryStream, but if we're dealing with a type derived from MemoryStream, Read(Span<byte>) will end up delegating
                // to Read(byte[], ...), which requires it to get a byte[] from ArrayPool and copy the data.  So, we special-case the
                // very common case of the Memory<byte> wrapping an array: if it does, we delegate to Read(byte[], ...) with it,
                // as that will be efficient in both cases, and we fall back to Read(Span<byte>) if the Memory<byte> wrapped something
                // else; if this is a concrete MemoryStream, that'll be efficient, and only in the case where the Memory<byte> wrapped
                // something other than an array and this is a MemoryStream-derived type that doesn't override Read(Span<byte>) will
                // it then fall back to doing the ArrayPool/copy behavior.
                return new ValueTask<int>(
                    MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> destinationArray) ?
                        Read(destinationArray.Array, destinationArray.Offset, destinationArray.Count) :
                        Read(buffer.Span));
            }
            catch (OperationCanceledException oce) {
                return new ValueTask<int>();
            }
            catch (Exception exception) {
                return new ValueTask<int>();
            }
        }

        public override int ReadByte() {
            EnsureNotClosed();

            if (_position >= _length)
                return -1;

            return _buffer[_position++];
        }

        public override void CopyTo(Stream destination, int bufferSize) {
            
            // If we have been inherited into a subclass, the following implementation could be incorrect
            // since it does not call through to Read() which a subclass might have overridden.  
            // To be safe we will only use this implementation in cases where we know it is safe to do so,
            // and delegate to our base class (which will call into Read) when we are not sure.
            if (GetType() != typeof(MemoryStream)) {
                base.CopyTo(destination, bufferSize);
                return;
            }

            int originalPosition = _position;

            // Seek to the end of the MemoryStream.
            int remaining = InternalEmulateRead(_length - originalPosition);

            // If we were already at or past the end, there's no copying to do so just quit.
            if (remaining > 0) {
                // Call Write() on the other Stream, using our internal buffer and avoiding any
                // intermediary allocations.
                destination.Write(_buffer, originalPosition, remaining);
            }
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) {
            // This implementation offers better performance compared to the base class version.

            
            // If we have been inherited into a subclass, the following implementation could be incorrect
            // since it does not call through to ReadAsync() which a subclass might have overridden.  
            // To be safe we will only use this implementation in cases where we know it is safe to do so,
            // and delegate to our base class (which will call into ReadAsync) when we are not sure.
            if (GetType() != typeof(MemoryStream))
                return base.CopyToAsync(destination, bufferSize, cancellationToken);

            // If cancelled - return fast:
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            // Avoid copying data from this buffer into a temp buffer:
            //   (require that InternalEmulateRead does not throw,
            //    otherwise it needs to be wrapped into try-catch-Task.FromException like memStrDest.Write below)

            int pos = _position;
            int n = InternalEmulateRead(_length - _position);

            // If we were already at or past the end, there's no copying to do so just quit.
            if (n == 0)
                return Task.CompletedTask;

            // If destination is not a memory stream, write there asynchronously:
            MemoryStream memStrDest = destination as MemoryStream;
            if (memStrDest == null)
                return destination.WriteAsync(_buffer, pos, n, cancellationToken);

            try {
                // If destination is a MemoryStream, CopyTo synchronously:
                memStrDest.Write(_buffer, pos, n);
                return Task.CompletedTask;
            }
            catch (Exception ex) {
                return Task.FromException(ex);
            }
        }


        public override long Seek(long offset, SeekOrigin loc) {
            EnsureNotClosed();


            switch (loc) {
                case SeekOrigin.Begin: {
                        int tempPosition = unchecked(_origin + (int)offset);
                        _position = tempPosition;
                        break;
                    }
                case SeekOrigin.Current: {
                        int tempPosition = unchecked(_position + (int)offset);
                        _position = tempPosition;
                        break;
                    }
                case SeekOrigin.End: {
                        int tempPosition = unchecked(_length + (int)offset);
                        _position = tempPosition;
                        break;
                    }
                default:
                    throw new ArgumentException("loc");
            }

            Debug.Assert(_position >= 0, "_position >= 0");
            return _position;
        }

        // Sets the length of the stream to a given value.  The new
        // value must be nonnegative and less than the space remaining in
        // the array, int.MaxValue - origin
        // Origin is 0 in all cases other than a MemoryStream created on
        // top of an existing array and a specific starting offset was passed 
        // into the MemoryStream constructor.  The upper bounds prevents any 
        // situations where a stream may be created on top of an array then 
        // the stream is made longer than the maximum possible length of the 
        // array (int.MaxValue).
        // 
        public override void SetLength(long value) {

            EnsureWriteable();

            // Origin wasn't publicly exposed above.
            Debug.Assert(MemStreamMaxLength == int.MaxValue);  // Check parameter validation logic in this method if this fails.

            int newLength = _origin + (int)value;
            bool allocatedNewArray = EnsureCapacity(newLength);
            if (!allocatedNewArray && newLength > _length)
                Array.Clear(_buffer, _length, newLength - _length);
            _length = newLength;
            if (_position > newLength)
                _position = newLength;
        }

        public virtual byte[] ToArray() {
            int count = _length - _origin;
            if (count == 0)
                return Array.Empty<byte>();
            byte[] copy = new byte[count];
            Buffer.BlockCopy(_buffer, _origin, copy, 0, count);
            return copy;
        }

        public override void Write(byte[] buffer, int offset, int count) {
            

            int i = _position + count;
            // Check for overflow
            
            if (i > _length) {
                bool mustZero = _position > _length;
                if (i > _capacity) {
                    bool allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray) {
                        mustZero = false;
                    }
                }
                if (mustZero) {
                    Array.Clear(_buffer, _length, i - _length);
                }
                _length = i;
            }
            if ((count <= 8) && (buffer != _buffer)) {
                int byteCount = count;
                while (--byteCount >= 0) {
                    _buffer[_position + byteCount] = buffer[offset + byteCount];
                }
            }
            else {
                Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
            }
            _position = i;
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            if (GetType() != typeof(MemoryStream)) {
                // MemoryStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this Write(Span<byte>) overload being introduced.  In that case, this Write(Span<byte>) overload
                // should use the behavior of Write(byte[],int,int) overload.
                base.Write(buffer);
                return;
            }

            EnsureNotClosed();
            EnsureWriteable();

            // Check for overflow
            int i = _position + buffer.Length;

            if (i > _length) {
                bool mustZero = _position > _length;
                if (i > _capacity) {
                    bool allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray) {
                        mustZero = false;
                    }
                }
                if (mustZero) {
                    Array.Clear(_buffer, _length, i - _length);
                }
                _length = i;
            }

            buffer.CopyTo(new Span<byte>(_buffer, _position, buffer.Length));
            _position = i;
        }

        public override Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            
            // If cancellation is already requested, bail early
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            try {
                Write(buffer, offset, count);
                return Task.CompletedTask;
            }
            catch (Exception exception) {
                return Task.FromException(exception);
            }
        }

        public override Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken)) {
                // See corresponding comment in ReadAsync for why we don't just always use Write(ReadOnlySpan<byte>).
                // Unlike ReadAsync, we could delegate to WriteAsync(byte[], ...) here, but we don't for consistency.
                if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> sourceArray)) {
                    Write(sourceArray.Array, sourceArray.Offset, sourceArray.Count);
                }
                else {
                    Write(buffer.Span);
                }
                return Task.CompletedTask;
        }

        public override void WriteByte(byte value) {
            if (_position >= _length) {
                int newLength = _position + 1;
                bool mustZero = _position > _length;
                if (newLength >= _capacity) {
                    bool allocatedNewArray = EnsureCapacity(newLength);
                    if (allocatedNewArray) {
                        mustZero = false;
                    }
                }
                if (mustZero) {
                    Array.Clear(_buffer, _length, _position - _length);
                }
                _length = newLength;
            }
            _buffer[_position++] = value;
        }

        // Writes this MemoryStream to another stream.
        public virtual void WriteTo(Stream stream) {
            
            EnsureNotClosed();

            stream.Write(_buffer, _origin, _length - _origin);
        }
    }
}