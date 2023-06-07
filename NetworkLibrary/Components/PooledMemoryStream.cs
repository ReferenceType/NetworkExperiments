﻿using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Components
{
    /*There is no allccation here, all byte arrays comes from pool and retuned on flush */
    public class PooledMemoryStream : Stream
    {
        byte[] bufferInternal;
        public PooledMemoryStream()
        {
            bufferInternal = BufferPool.RentBuffer(512);
        }

        public PooledMemoryStream(int minCapacity)
        {
            bufferInternal = BufferPool.RentBuffer(minCapacity);
        }
       
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        private int length;

        private int position = 0;
        private int _origin = 0;
        private int _capacity => bufferInternal.Length;

        public override long Position
        {
            get => position;
            set
            {
                if (bufferInternal.Length < value)
                    ExpandInternalBuffer((int)value);

                position = (int)value;
                
            }
        }

        public int Position32
        {
            get => position;
            set
            {
                if (bufferInternal.Length < value)
                    ExpandInternalBuffer(value);

                position = value;

            }
        }

        public override long Length { get => length;  }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {


        }

        public void Clear()
        {
            position = 0;
            if (bufferInternal.Length > 65537)
            {
                BufferPool.ReturnBuffer(bufferInternal);
                bufferInternal = BufferPool.RentBuffer(64000);
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = count>_capacity-position? (int)_capacity - (int)position : count;
            unsafe
            {
                fixed (byte* destination = &buffer[offset])
                {
                    fixed (byte* toCopy = &bufferInternal[position])
                        Buffer.MemoryCopy(toCopy, destination, count, count);
                }
                Position += count;
                return count;
            }

        }
     
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset > BufferPool.MaxBufferSize)
                throw new ArgumentOutOfRangeException("offset");
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        int tempPosition = unchecked( (int)offset);
                        if (offset < 0 || tempPosition < 0)
                            throw new IOException("IO.IO_SeekBeforeBegin");
                        Position = tempPosition;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        int tempPosition = unchecked(position + (int)offset);
                        if (unchecked(position + offset) < _origin || tempPosition < _origin)
                            throw new IOException("IO.IO_SeekBeforeBegin");
                        Position = tempPosition;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        int tempPosition = unchecked(length + (int)offset);
                        if (unchecked(length + offset) < _origin || tempPosition < _origin)
                            throw new IOException("IO.IO_SeekBeforeBegin");
                        Position = tempPosition;
                        break;
                    }
                default:
                    throw new ArgumentException("Argument_InvalidSeekOrigin");
            }

           
            return position;

        }

        public override void SetLength(long value)
        {
            if (_capacity < value)
                ExpandInternalBuffer((int)value);
            length = (int)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetBuffer() => bufferInternal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (bufferInternal.Length - position < count)
            {
                int demandSize = count + (bufferInternal.Length);
                if (demandSize > BufferPool.MaxBufferSize)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                else
                    ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            }
            
            //if (count < 8)
            //{
            //    int byteCount = count;
            //    while (--byteCount >= 0)
            //        bufferInternal[position + byteCount] = buffer[offset + byteCount];
            //}
            //else
            //{
            //    unsafe
            //    {
            //        fixed (byte* destination = &bufferInternal[position])
            //        {
            //            fixed (byte* toCopy = &buffer[offset])
            //                Buffer.MemoryCopy(toCopy, destination, count, count);
            //        }
            //    }
            //    position += count;
            //}

            unsafe
            {
                fixed (byte* destination = &bufferInternal[position])
                {
                    fixed (byte* toCopy = &buffer[offset])
                        Buffer.MemoryCopy(toCopy, destination, count, count);
                }
            }
            position += count;


            if (length<position)
                length = position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandInternalBuffer(int size)
        {
            if (size <= bufferInternal.Length)
                throw new InvalidOperationException("Cannot expand internal buffer to smaller size");


            var newBuf = BufferPool.RentBuffer(size);
            if (position > 0)
            {
                unsafe
                {
                    fixed (byte* destination = newBuf)
                    {
                        fixed (byte* toCopy = bufferInternal)
                            Buffer.MemoryCopy(toCopy, destination, position, position);
                    }
                }
            }

            BufferPool.ReturnBuffer(bufferInternal);
            bufferInternal = newBuf;
        }

        public void Reserve(int count)
        {
            if (bufferInternal.Length - position < count)
            {
                int demandSize = count + (bufferInternal.Length);
                if (demandSize > BufferPool.MaxBufferSize)
                    throw new InvalidOperationException($"Cannot expand internal buffer to more than max amount: {BufferPool.MaxBufferSize}");
                else
                    ExpandInternalBuffer(demandSize);
            }
        }

        public override void WriteByte(byte value)
        {
            if (bufferInternal.Length - position < 1)
            {
                int demandSize = 1 + (bufferInternal.Length);
                if (demandSize > BufferPool.MaxBufferSize)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                else
                    ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            }

            bufferInternal[position++] = value;
            if (length < position)
                length = position;
        }
        public override int ReadByte()
        {
            if (position >= length) return -1;
            return bufferInternal[position++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteIntUnchecked(int value)
        {
            //if (bufferInternal.Length - position < 4)
            //{
            //    int demandSize = 4 + (bufferInternal.Length);
            //    if (demandSize > BufferPool.MaxBufferSize)
            //        throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
            //    else
            //        ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            //}
            unsafe
            {
                fixed (byte* b = &bufferInternal[position])
                    *(int*)b = value;
            }
            position += 4;
          
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteInt(int value)
        {
            if (bufferInternal.Length - position < 4)
            {
                int demandSize = 4 + (bufferInternal.Length);
                if (demandSize > BufferPool.MaxBufferSize)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                else
                    ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            }
            unsafe
            {
                fixed (byte* b = &bufferInternal[position])
                    *(int*)b = value;
            }
            position += 4;


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUshortUnchecked(ushort value)
        {
            //if (bufferInternal.Length - position < 2)
            //{
            //    int demandSize = 2 + (bufferInternal.Length);
            //    if (demandSize > BufferPool.MaxBufferSize)
            //        throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
            //    else
            //        ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            //}
            unsafe
            {
                fixed (byte* b = &bufferInternal[position])
                    *(short*)b = (short)value;
            }
            position += 2;

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUshort(ushort value)
        {
            //if (bufferInternal.Length - position < 2)
            //{
            //    int demandSize = 2 + (bufferInternal.Length);
            //    if (demandSize > BufferPool.MaxBufferSize)
            //        throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
            //    else
            //        ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            //}
            unsafe
            {
                fixed (byte* b = &bufferInternal[position])
                    *(short*)b = (short)value;
            }
            position += 2;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTwoZerosUnchecked()
        {
            //if (bufferInternal.Length - position < 2)
            //{
            //    int demandSize = 2 + (bufferInternal.Length);
            //    if (demandSize > BufferPool.MaxBufferSize)
            //        throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
            //    else
            //        ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            //}

            bufferInternal[position] = 0;
            bufferInternal[position+1] = 0;
            position+= 2;
            
        }


        /// <summary>
        /// Gets a memory region from stream internal buffer, after the current position.
        /// Size is atleast the amount hinted, and minimum is 256 bytes
        /// </summary>
        /// <param name="sizeHint"></param>
        /// <param name="buff"></param>
        /// <param name="offst"></param>
        /// <param name="cnt"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void GetMemory(int sizeHint, out byte[] buff, out int offst, out int cnt)
        {
            if (sizeHint < 128)
                sizeHint = 128;

            if (bufferInternal.Length - position < sizeHint)
            {
                int demandSize = sizeHint + (bufferInternal.Length);
                if (demandSize > BufferPool.MaxBufferSize)
                    throw new InvalidOperationException($"Cannot expand internal buffer to more than max amount: {BufferPool.MaxBufferSize}");
                else
                    ExpandInternalBuffer(demandSize);
            }

            buff = bufferInternal;
            offst = (int)position;
            cnt = sizeHint;
        }

        internal void Advance(int amount)
        {
            position +=amount;
            if (length < position)
                length = position;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BufferPool.ReturnBuffer(bufferInternal);
                bufferInternal = null;
            }
            base.Dispose(disposing);
        }
    }
}
