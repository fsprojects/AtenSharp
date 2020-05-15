using System;
using System.Buffers;
using System.Linq;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using AtenSharp;

namespace AtenSharp.Numerics.Tensors
{

    /// <summary>
    ///   Wrapper class used to surface a Aten ByteTensor as a System.Numerics DensorTensor of byte
    /// </summary>
    public sealed class ByteAtenTensor : DenseTensor<byte>, IDisposable
    {
        internal sealed class ByteNativeMemory : MemoryManager<byte>
        {
            private readonly ByteTensor.ByteStorage storage;

            public ByteNativeMemory(ByteTensor.ByteStorage storage)
            {
                if (storage.Size < 0)
                {
                    throw new ArgumentOutOfRangeException ("Length cannot be negative.");
                }

                storage.Retain ();
                this.storage = storage;
            }

            /// <summary>
            /// Returns a span wrapping the underlying memory.
            /// Remember to Unpin the memory once the span is disposed.
            /// </summary>
            public override Span<byte> GetSpan ()
            {
                ulong len = storage.Size;

                if (len > int.MaxValue)
                {
                    throw new InvalidCastException ("Tensor size not supported.");
                }

                unsafe
                {
                    return new Span<byte> (storage.Data.ToPointer (), (int)len);
                }
            }

            /// <summary>
            /// Returns a handle to the memory that has been pinned and hence its address can be taken.
            /// </summary>
            /// <param name="elementIndex">The offset to the element within the memory at which the returned <see cref="MemoryHandle"/> points to. (default = 0)</param>
            public override MemoryHandle Pin (int elementIndex = 0)
            {
                if ((uint)elementIndex > storage.Size)
                {
                    throw new ArgumentOutOfRangeException (nameof (elementIndex), "Index out of array bound.");
                }

                unsafe
                {
                    storage.Retain();

                    void* pointer = Unsafe.Add<byte> ((void*)storage.Data, elementIndex);
                    return new MemoryHandle (pointer, default, this);
                }
            }

            /// <summary>
            /// Lets the garbage collector know that the object is free to be moved now.
            /// </summary>
            public override void Unpin ()
            {
                storage.Free ();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    storage.Free ();
                }
            }
        }

        private readonly ByteTensor inner;

        /// <summary>
        ///   Property returning the inner AtenSharp tensor the class is wrapping.
        /// </summary>
        public ByteTensor AtenSharpTensor => inner;

        public ByteAtenTensor (Memory<byte> memory, ReadOnlySpan<int> dimensions, ByteTensor inner) : base (memory, dimensions)
        {
            this.inner = inner;
        }

        /// <summary>
        ///   Utility method to create a AtenTensor.
        ///   This is currently failing if the input parameter is empty because AtenSharp.Numerics.Tensors
        ///   does not support zero-size tensors.
        /// </summary>
        /// <param name="sizes">The desired sizes for the dimensions of the tensor.</param>
        public static ByteAtenTensor Create (params int[] sizes)
        {
            var totLength = Utils.GetTotalLength (sizes);
            var shape = sizes;

            if (sizes.Length == 0)
            {
                shape = new int[] { 0 };
            }

            var inner = CreateByteTensor (sizes.Select (x => (long)x).ToArray ());
            var mem = new ByteNativeMemory (inner.Storage);

            return new ByteAtenTensor (mem.Memory, shape, inner);
        }

        public void Dispose ()
        {
            inner.Dispose ();
        }

        /// <summary>
        /// Creates a shallow copy of this tensor, with new backing storage.
        /// </summary>
        /// <returns>A shallow copy of this tensor.</returns>
        public override Tensor<byte> Clone ()
        {
            var innerClone = inner.Clone ();
            var mem = new ByteNativeMemory (innerClone.Storage);

            return new ByteAtenTensor (mem.Memory, Dimensions, innerClone);
        }

        /// <summary>
        /// Creates a new Tensor of a different type with the specified dimensions and the same layout as this tensor with elements initialized to their default value.
        /// </summary>
        /// <typeparam name="TResult">Type contained in the returned Tensor.</typeparam>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor with the same layout as this tensor but different type and dimensions.</returns>
        public override Tensor<TResult> CloneEmpty<TResult> (ReadOnlySpan<int> dimensions)
        {
            switch (true)
            {
                case bool _ when typeof (TResult) == typeof (byte):
                {
                    var innerClone = ByteAtenTensor.CreateByteTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ByteAtenTensor.ByteNativeMemory (innerClone.Storage);

                    return new ByteAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (short):
                {
                    var innerClone = ShortAtenTensor.CreateShortTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ShortAtenTensor.ShortNativeMemory (innerClone.Storage);

                    return new ShortAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (int):
                {
                    var innerClone = IntAtenTensor.CreateIntTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new IntAtenTensor.IntNativeMemory (innerClone.Storage);

                    return new IntAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (long):
                {
                    var innerClone = LongAtenTensor.CreateLongTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new LongAtenTensor.LongNativeMemory (innerClone.Storage);

                    return new LongAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (double):
                {
                    var innerClone = DoubleAtenTensor.CreateDoubleTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new DoubleAtenTensor.DoubleNativeMemory (innerClone.Storage);

                    return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (float):
                {
                    var innerClone = FloatAtenTensor.CreateFloatTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new FloatAtenTensor.FloatNativeMemory (innerClone.Storage);

                    return new FloatAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                default: throw new NotImplementedException ($"Cloning type {typeof (TResult)} is not supported.");
            }
        }

        /// <summary>
        /// Reshapes the current tensor to new dimensions, using the same backing storage.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor that reinterprets backing Buffer of this tensor with different dimensions.</returns>
        public override Tensor<byte> Reshape (ReadOnlySpan<int> dimensions)
        {
            if (dimensions.Length == 0)
            {
                throw new ArgumentException ("Dimensions must contain elements.", nameof (dimensions));
            }

            var newSize = Utils.GetTotalLength (dimensions);

            if (newSize != Length)
            {
                throw new ArgumentException ($"Cannot reshape array due to mismatch in lengths, currently {Length} would become {newSize}.", nameof(dimensions));
            }

            ByteTensor reshapedTensor;

            switch (dimensions.Length)
            {
                case 1:
                    reshapedTensor = inner.NewWithStorage1d (
                        UIntPtr.Zero,
                        dimensions[0],
                        1);
                    break;
                case 2:
                    reshapedTensor = inner.NewWithStorage2d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1],
                        dimensions[1],
                        1);
                    break;
                case 3:
                    reshapedTensor = inner.NewWithStorage3d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1] + dimensions[2],
                        dimensions[1],
                        dimensions[2],
                        dimensions[2],
                        1);
                    break;
                case 4:
                    reshapedTensor = inner.NewWithStorage4d (
                        UIntPtr.Zero,
                        dimensions[0], dimensions[1] + dimensions[2] + dimensions[3],
                        dimensions[1],
                        dimensions[2] + dimensions[3],
                        dimensions[2],
                        dimensions[3],
                        dimensions[3],
                        1);
                    break;
                 default: throw new ArgumentException ($"Cannot reshape tensor with more than 4 dimensions");
            }

            return new ByteAtenTensor (Buffer, dimensions, reshapedTensor);
        }

        /// <summary>
        ///   Creates a 1-4D tensor of the specified size(s).
        /// </summary>
        /// <param name="dims">Sizes for the dimensions.</param>
        internal static ByteTensor CreateByteTensor (params long[] dims)
        {
            switch (dims.Length)
            {
                case 0:
                    return new ByteTensor ();
                case 1:
                    return new ByteTensor (dims[0]);
                case 2:
                    return new ByteTensor (dims[0], dims[1]);
                case 3:
                    return new ByteTensor (dims[0], dims[1], dims[2]);
                case 4:
                    return new ByteTensor (dims[0], dims[1], dims[2], dims[3]);
                default:
                    throw new ArgumentOutOfRangeException (nameof (dims), "Maximum number of dimensions for tensor creation is 4.");
            }
        }
    }

    /// <summary>
    ///   Wrapper class used to surface a Aten ShortTensor as a System.Numerics DensorTensor of short
    /// </summary>
    public sealed class ShortAtenTensor : DenseTensor<short>, IDisposable
    {
        internal sealed class ShortNativeMemory : MemoryManager<short>
        {
            private readonly ShortTensor.ShortStorage storage;

            public ShortNativeMemory(ShortTensor.ShortStorage storage)
            {
                if (storage.Size < 0)
                {
                    throw new ArgumentOutOfRangeException ("Length cannot be negative.");
                }

                storage.Retain ();
                this.storage = storage;
            }

            /// <summary>
            /// Returns a span wrapping the underlying memory.
            /// Remember to Unpin the memory once the span is disposed.
            /// </summary>
            public override Span<short> GetSpan ()
            {
                ulong len = storage.Size;

                if (len > int.MaxValue)
                {
                    throw new InvalidCastException ("Tensor size not supported.");
                }

                unsafe
                {
                    return new Span<short> (storage.Data.ToPointer (), (int)len);
                }
            }

            /// <summary>
            /// Returns a handle to the memory that has been pinned and hence its address can be taken.
            /// </summary>
            /// <param name="elementIndex">The offset to the element within the memory at which the returned <see cref="MemoryHandle"/> points to. (default = 0)</param>
            public override MemoryHandle Pin (int elementIndex = 0)
            {
                if ((uint)elementIndex > storage.Size)
                {
                    throw new ArgumentOutOfRangeException (nameof (elementIndex), "Index out of array bound.");
                }

                unsafe
                {
                    storage.Retain();

                    void* pointer = Unsafe.Add<short> ((void*)storage.Data, elementIndex);
                    return new MemoryHandle (pointer, default, this);
                }
            }

            /// <summary>
            /// Lets the garbage collector know that the object is free to be moved now.
            /// </summary>
            public override void Unpin ()
            {
                storage.Free ();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    storage.Free ();
                }
            }
        }

        private readonly ShortTensor inner;

        /// <summary>
        ///   Property returning the inner AtenSharp tensor the class is wrapping.
        /// </summary>
        public ShortTensor AtenSharpTensor => inner;

        public ShortAtenTensor (Memory<short> memory, ReadOnlySpan<int> dimensions, ShortTensor inner) : base (memory, dimensions)
        {
            this.inner = inner;
        }

        /// <summary>
        ///   Utility method to create a AtenTensor.
        ///   This is currently failing if the input parameter is empty because AtenSharp.Numerics.Tensors
        ///   does not support zero-size tensors.
        /// </summary>
        /// <param name="sizes">The desired sizes for the dimensions of the tensor.</param>
        public static ShortAtenTensor Create (params int[] sizes)
        {
            var totLength = Utils.GetTotalLength (sizes);
            var shape = sizes;

            if (sizes.Length == 0)
            {
                shape = new int[] { 0 };
            }

            var inner = CreateShortTensor (sizes.Select (x => (long)x).ToArray ());
            var mem = new ShortNativeMemory (inner.Storage);

            return new ShortAtenTensor (mem.Memory, shape, inner);
        }

        public void Dispose ()
        {
            inner.Dispose ();
        }

        /// <summary>
        /// Creates a shallow copy of this tensor, with new backing storage.
        /// </summary>
        /// <returns>A shallow copy of this tensor.</returns>
        public override Tensor<short> Clone ()
        {
            var innerClone = inner.Clone ();
            var mem = new ShortNativeMemory (innerClone.Storage);

            return new ShortAtenTensor (mem.Memory, Dimensions, innerClone);
        }

        /// <summary>
        /// Creates a new Tensor of a different type with the specified dimensions and the same layout as this tensor with elements initialized to their default value.
        /// </summary>
        /// <typeparam name="TResult">Type contained in the returned Tensor.</typeparam>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor with the same layout as this tensor but different type and dimensions.</returns>
        public override Tensor<TResult> CloneEmpty<TResult> (ReadOnlySpan<int> dimensions)
        {
            switch (true)
            {
                case bool _ when typeof (TResult) == typeof (byte):
                {
                    var innerClone = ByteAtenTensor.CreateByteTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ByteAtenTensor.ByteNativeMemory (innerClone.Storage);

                    return new ByteAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (short):
                {
                    var innerClone = ShortAtenTensor.CreateShortTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ShortAtenTensor.ShortNativeMemory (innerClone.Storage);

                    return new ShortAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (int):
                {
                    var innerClone = IntAtenTensor.CreateIntTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new IntAtenTensor.IntNativeMemory (innerClone.Storage);

                    return new IntAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (long):
                {
                    var innerClone = LongAtenTensor.CreateLongTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new LongAtenTensor.LongNativeMemory (innerClone.Storage);

                    return new LongAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (double):
                {
                    var innerClone = DoubleAtenTensor.CreateDoubleTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new DoubleAtenTensor.DoubleNativeMemory (innerClone.Storage);

                    return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (float):
                {
                    var innerClone = FloatAtenTensor.CreateFloatTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new FloatAtenTensor.FloatNativeMemory (innerClone.Storage);

                    return new FloatAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                default: throw new NotImplementedException ($"Cloning type {typeof (TResult)} is not supported.");
            }
        }

        /// <summary>
        /// Reshapes the current tensor to new dimensions, using the same backing storage.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor that reinterprets backing Buffer of this tensor with different dimensions.</returns>
        public override Tensor<short> Reshape (ReadOnlySpan<int> dimensions)
        {
            if (dimensions.Length == 0)
            {
                throw new ArgumentException ("Dimensions must contain elements.", nameof (dimensions));
            }

            var newSize = Utils.GetTotalLength (dimensions);

            if (newSize != Length)
            {
                throw new ArgumentException ($"Cannot reshape array due to mismatch in lengths, currently {Length} would become {newSize}.", nameof(dimensions));
            }

            ShortTensor reshapedTensor;

            switch (dimensions.Length)
            {
                case 1:
                    reshapedTensor = inner.NewWithStorage1d (
                        UIntPtr.Zero,
                        dimensions[0],
                        1);
                    break;
                case 2:
                    reshapedTensor = inner.NewWithStorage2d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1],
                        dimensions[1],
                        1);
                    break;
                case 3:
                    reshapedTensor = inner.NewWithStorage3d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1] + dimensions[2],
                        dimensions[1],
                        dimensions[2],
                        dimensions[2],
                        1);
                    break;
                case 4:
                    reshapedTensor = inner.NewWithStorage4d (
                        UIntPtr.Zero,
                        dimensions[0], dimensions[1] + dimensions[2] + dimensions[3],
                        dimensions[1],
                        dimensions[2] + dimensions[3],
                        dimensions[2],
                        dimensions[3],
                        dimensions[3],
                        1);
                    break;
                 default: throw new ArgumentException ($"Cannot reshape tensor with more than 4 dimensions");
            }

            return new ShortAtenTensor (Buffer, dimensions, reshapedTensor);
        }

        /// <summary>
        ///   Creates a 1-4D tensor of the specified size(s).
        /// </summary>
        /// <param name="dims">Sizes for the dimensions.</param>
        internal static ShortTensor CreateShortTensor (params long[] dims)
        {
            switch (dims.Length)
            {
                case 0:
                    return new ShortTensor ();
                case 1:
                    return new ShortTensor (dims[0]);
                case 2:
                    return new ShortTensor (dims[0], dims[1]);
                case 3:
                    return new ShortTensor (dims[0], dims[1], dims[2]);
                case 4:
                    return new ShortTensor (dims[0], dims[1], dims[2], dims[3]);
                default:
                    throw new ArgumentOutOfRangeException (nameof (dims), "Maximum number of dimensions for tensor creation is 4.");
            }
        }
    }

    /// <summary>
    ///   Wrapper class used to surface a Aten IntTensor as a System.Numerics DensorTensor of int
    /// </summary>
    public sealed class IntAtenTensor : DenseTensor<int>, IDisposable
    {
        internal sealed class IntNativeMemory : MemoryManager<int>
        {
            private readonly IntTensor.IntStorage storage;

            public IntNativeMemory(IntTensor.IntStorage storage)
            {
                if (storage.Size < 0)
                {
                    throw new ArgumentOutOfRangeException ("Length cannot be negative.");
                }

                storage.Retain ();
                this.storage = storage;
            }

            /// <summary>
            /// Returns a span wrapping the underlying memory.
            /// Remember to Unpin the memory once the span is disposed.
            /// </summary>
            public override Span<int> GetSpan ()
            {
                ulong len = storage.Size;

                if (len > int.MaxValue)
                {
                    throw new InvalidCastException ("Tensor size not supported.");
                }

                unsafe
                {
                    return new Span<int> (storage.Data.ToPointer (), (int)len);
                }
            }

            /// <summary>
            /// Returns a handle to the memory that has been pinned and hence its address can be taken.
            /// </summary>
            /// <param name="elementIndex">The offset to the element within the memory at which the returned <see cref="MemoryHandle"/> points to. (default = 0)</param>
            public override MemoryHandle Pin (int elementIndex = 0)
            {
                if ((uint)elementIndex > storage.Size)
                {
                    throw new ArgumentOutOfRangeException (nameof (elementIndex), "Index out of array bound.");
                }

                unsafe
                {
                    storage.Retain();

                    void* pointer = Unsafe.Add<int> ((void*)storage.Data, elementIndex);
                    return new MemoryHandle (pointer, default, this);
                }
            }

            /// <summary>
            /// Lets the garbage collector know that the object is free to be moved now.
            /// </summary>
            public override void Unpin ()
            {
                storage.Free ();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    storage.Free ();
                }
            }
        }

        private readonly IntTensor inner;

        /// <summary>
        ///   Property returning the inner AtenSharp tensor the class is wrapping.
        /// </summary>
        public IntTensor AtenSharpTensor => inner;

        public IntAtenTensor (Memory<int> memory, ReadOnlySpan<int> dimensions, IntTensor inner) : base (memory, dimensions)
        {
            this.inner = inner;
        }

        /// <summary>
        ///   Utility method to create a AtenTensor.
        ///   This is currently failing if the input parameter is empty because AtenSharp.Numerics.Tensors
        ///   does not support zero-size tensors.
        /// </summary>
        /// <param name="sizes">The desired sizes for the dimensions of the tensor.</param>
        public static IntAtenTensor Create (params int[] sizes)
        {
            var totLength = Utils.GetTotalLength (sizes);
            var shape = sizes;

            if (sizes.Length == 0)
            {
                shape = new int[] { 0 };
            }

            var inner = CreateIntTensor (sizes.Select (x => (long)x).ToArray ());
            var mem = new IntNativeMemory (inner.Storage);

            return new IntAtenTensor (mem.Memory, shape, inner);
        }

        public void Dispose ()
        {
            inner.Dispose ();
        }

        /// <summary>
        /// Creates a shallow copy of this tensor, with new backing storage.
        /// </summary>
        /// <returns>A shallow copy of this tensor.</returns>
        public override Tensor<int> Clone ()
        {
            var innerClone = inner.Clone ();
            var mem = new IntNativeMemory (innerClone.Storage);

            return new IntAtenTensor (mem.Memory, Dimensions, innerClone);
        }

        /// <summary>
        /// Creates a new Tensor of a different type with the specified dimensions and the same layout as this tensor with elements initialized to their default value.
        /// </summary>
        /// <typeparam name="TResult">Type contained in the returned Tensor.</typeparam>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor with the same layout as this tensor but different type and dimensions.</returns>
        public override Tensor<TResult> CloneEmpty<TResult> (ReadOnlySpan<int> dimensions)
        {
            switch (true)
            {
                case bool _ when typeof (TResult) == typeof (byte):
                {
                    var innerClone = ByteAtenTensor.CreateByteTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ByteAtenTensor.ByteNativeMemory (innerClone.Storage);

                    return new ByteAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (short):
                {
                    var innerClone = ShortAtenTensor.CreateShortTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ShortAtenTensor.ShortNativeMemory (innerClone.Storage);

                    return new ShortAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (int):
                {
                    var innerClone = IntAtenTensor.CreateIntTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new IntAtenTensor.IntNativeMemory (innerClone.Storage);

                    return new IntAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (long):
                {
                    var innerClone = LongAtenTensor.CreateLongTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new LongAtenTensor.LongNativeMemory (innerClone.Storage);

                    return new LongAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (double):
                {
                    var innerClone = DoubleAtenTensor.CreateDoubleTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new DoubleAtenTensor.DoubleNativeMemory (innerClone.Storage);

                    return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (float):
                {
                    var innerClone = FloatAtenTensor.CreateFloatTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new FloatAtenTensor.FloatNativeMemory (innerClone.Storage);

                    return new FloatAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                default: throw new NotImplementedException ($"Cloning type {typeof (TResult)} is not supported.");
            }
        }

        /// <summary>
        /// Reshapes the current tensor to new dimensions, using the same backing storage.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor that reinterprets backing Buffer of this tensor with different dimensions.</returns>
        public override Tensor<int> Reshape (ReadOnlySpan<int> dimensions)
        {
            if (dimensions.Length == 0)
            {
                throw new ArgumentException ("Dimensions must contain elements.", nameof (dimensions));
            }

            var newSize = Utils.GetTotalLength (dimensions);

            if (newSize != Length)
            {
                throw new ArgumentException ($"Cannot reshape array due to mismatch in lengths, currently {Length} would become {newSize}.", nameof(dimensions));
            }

            IntTensor reshapedTensor;

            switch (dimensions.Length)
            {
                case 1:
                    reshapedTensor = inner.NewWithStorage1d (
                        UIntPtr.Zero,
                        dimensions[0],
                        1);
                    break;
                case 2:
                    reshapedTensor = inner.NewWithStorage2d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1],
                        dimensions[1],
                        1);
                    break;
                case 3:
                    reshapedTensor = inner.NewWithStorage3d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1] + dimensions[2],
                        dimensions[1],
                        dimensions[2],
                        dimensions[2],
                        1);
                    break;
                case 4:
                    reshapedTensor = inner.NewWithStorage4d (
                        UIntPtr.Zero,
                        dimensions[0], dimensions[1] + dimensions[2] + dimensions[3],
                        dimensions[1],
                        dimensions[2] + dimensions[3],
                        dimensions[2],
                        dimensions[3],
                        dimensions[3],
                        1);
                    break;
                 default: throw new ArgumentException ($"Cannot reshape tensor with more than 4 dimensions");
            }

            return new IntAtenTensor (Buffer, dimensions, reshapedTensor);
        }

        /// <summary>
        ///   Creates a 1-4D tensor of the specified size(s).
        /// </summary>
        /// <param name="dims">Sizes for the dimensions.</param>
        internal static IntTensor CreateIntTensor (params long[] dims)
        {
            switch (dims.Length)
            {
                case 0:
                    return new IntTensor ();
                case 1:
                    return new IntTensor (dims[0]);
                case 2:
                    return new IntTensor (dims[0], dims[1]);
                case 3:
                    return new IntTensor (dims[0], dims[1], dims[2]);
                case 4:
                    return new IntTensor (dims[0], dims[1], dims[2], dims[3]);
                default:
                    throw new ArgumentOutOfRangeException (nameof (dims), "Maximum number of dimensions for tensor creation is 4.");
            }
        }
    }

    /// <summary>
    ///   Wrapper class used to surface a Aten LongTensor as a System.Numerics DensorTensor of long
    /// </summary>
    public sealed class LongAtenTensor : DenseTensor<long>, IDisposable
    {
        internal sealed class LongNativeMemory : MemoryManager<long>
        {
            private readonly LongTensor.LongStorage storage;

            public LongNativeMemory(LongTensor.LongStorage storage)
            {
                if (storage.Size < 0)
                {
                    throw new ArgumentOutOfRangeException ("Length cannot be negative.");
                }

                storage.Retain ();
                this.storage = storage;
            }

            /// <summary>
            /// Returns a span wrapping the underlying memory.
            /// Remember to Unpin the memory once the span is disposed.
            /// </summary>
            public override Span<long> GetSpan ()
            {
                ulong len = storage.Size;

                if (len > int.MaxValue)
                {
                    throw new InvalidCastException ("Tensor size not supported.");
                }

                unsafe
                {
                    return new Span<long> (storage.Data.ToPointer (), (int)len);
                }
            }

            /// <summary>
            /// Returns a handle to the memory that has been pinned and hence its address can be taken.
            /// </summary>
            /// <param name="elementIndex">The offset to the element within the memory at which the returned <see cref="MemoryHandle"/> points to. (default = 0)</param>
            public override MemoryHandle Pin (int elementIndex = 0)
            {
                if ((uint)elementIndex > storage.Size)
                {
                    throw new ArgumentOutOfRangeException (nameof (elementIndex), "Index out of array bound.");
                }

                unsafe
                {
                    storage.Retain();

                    void* pointer = Unsafe.Add<long> ((void*)storage.Data, elementIndex);
                    return new MemoryHandle (pointer, default, this);
                }
            }

            /// <summary>
            /// Lets the garbage collector know that the object is free to be moved now.
            /// </summary>
            public override void Unpin ()
            {
                storage.Free ();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    storage.Free ();
                }
            }
        }

        private readonly LongTensor inner;

        /// <summary>
        ///   Property returning the inner AtenSharp tensor the class is wrapping.
        /// </summary>
        public LongTensor AtenSharpTensor => inner;

        public LongAtenTensor (Memory<long> memory, ReadOnlySpan<int> dimensions, LongTensor inner) : base (memory, dimensions)
        {
            this.inner = inner;
        }

        /// <summary>
        ///   Utility method to create a AtenTensor.
        ///   This is currently failing if the input parameter is empty because AtenSharp.Numerics.Tensors
        ///   does not support zero-size tensors.
        /// </summary>
        /// <param name="sizes">The desired sizes for the dimensions of the tensor.</param>
        public static LongAtenTensor Create (params int[] sizes)
        {
            var totLength = Utils.GetTotalLength (sizes);
            var shape = sizes;

            if (sizes.Length == 0)
            {
                shape = new int[] { 0 };
            }

            var inner = CreateLongTensor (sizes.Select (x => (long)x).ToArray ());
            var mem = new LongNativeMemory (inner.Storage);

            return new LongAtenTensor (mem.Memory, shape, inner);
        }

        public void Dispose ()
        {
            inner.Dispose ();
        }

        /// <summary>
        /// Creates a shallow copy of this tensor, with new backing storage.
        /// </summary>
        /// <returns>A shallow copy of this tensor.</returns>
        public override Tensor<long> Clone ()
        {
            var innerClone = inner.Clone ();
            var mem = new LongNativeMemory (innerClone.Storage);

            return new LongAtenTensor (mem.Memory, Dimensions, innerClone);
        }

        /// <summary>
        /// Creates a new Tensor of a different type with the specified dimensions and the same layout as this tensor with elements initialized to their default value.
        /// </summary>
        /// <typeparam name="TResult">Type contained in the returned Tensor.</typeparam>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor with the same layout as this tensor but different type and dimensions.</returns>
        public override Tensor<TResult> CloneEmpty<TResult> (ReadOnlySpan<int> dimensions)
        {
            switch (true)
            {
                case bool _ when typeof (TResult) == typeof (byte):
                {
                    var innerClone = ByteAtenTensor.CreateByteTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ByteAtenTensor.ByteNativeMemory (innerClone.Storage);

                    return new ByteAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (short):
                {
                    var innerClone = ShortAtenTensor.CreateShortTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ShortAtenTensor.ShortNativeMemory (innerClone.Storage);

                    return new ShortAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (int):
                {
                    var innerClone = IntAtenTensor.CreateIntTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new IntAtenTensor.IntNativeMemory (innerClone.Storage);

                    return new IntAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (long):
                {
                    var innerClone = LongAtenTensor.CreateLongTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new LongAtenTensor.LongNativeMemory (innerClone.Storage);

                    return new LongAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (double):
                {
                    var innerClone = DoubleAtenTensor.CreateDoubleTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new DoubleAtenTensor.DoubleNativeMemory (innerClone.Storage);

                    return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (float):
                {
                    var innerClone = FloatAtenTensor.CreateFloatTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new FloatAtenTensor.FloatNativeMemory (innerClone.Storage);

                    return new FloatAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                default: throw new NotImplementedException ($"Cloning type {typeof (TResult)} is not supported.");
            }
        }

        /// <summary>
        /// Reshapes the current tensor to new dimensions, using the same backing storage.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor that reinterprets backing Buffer of this tensor with different dimensions.</returns>
        public override Tensor<long> Reshape (ReadOnlySpan<int> dimensions)
        {
            if (dimensions.Length == 0)
            {
                throw new ArgumentException ("Dimensions must contain elements.", nameof (dimensions));
            }

            var newSize = Utils.GetTotalLength (dimensions);

            if (newSize != Length)
            {
                throw new ArgumentException ($"Cannot reshape array due to mismatch in lengths, currently {Length} would become {newSize}.", nameof(dimensions));
            }

            LongTensor reshapedTensor;

            switch (dimensions.Length)
            {
                case 1:
                    reshapedTensor = inner.NewWithStorage1d (
                        UIntPtr.Zero,
                        dimensions[0],
                        1);
                    break;
                case 2:
                    reshapedTensor = inner.NewWithStorage2d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1],
                        dimensions[1],
                        1);
                    break;
                case 3:
                    reshapedTensor = inner.NewWithStorage3d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1] + dimensions[2],
                        dimensions[1],
                        dimensions[2],
                        dimensions[2],
                        1);
                    break;
                case 4:
                    reshapedTensor = inner.NewWithStorage4d (
                        UIntPtr.Zero,
                        dimensions[0], dimensions[1] + dimensions[2] + dimensions[3],
                        dimensions[1],
                        dimensions[2] + dimensions[3],
                        dimensions[2],
                        dimensions[3],
                        dimensions[3],
                        1);
                    break;
                 default: throw new ArgumentException ($"Cannot reshape tensor with more than 4 dimensions");
            }

            return new LongAtenTensor (Buffer, dimensions, reshapedTensor);
        }

        /// <summary>
        ///   Creates a 1-4D tensor of the specified size(s).
        /// </summary>
        /// <param name="dims">Sizes for the dimensions.</param>
        internal static LongTensor CreateLongTensor (params long[] dims)
        {
            switch (dims.Length)
            {
                case 0:
                    return new LongTensor ();
                case 1:
                    return new LongTensor (dims[0]);
                case 2:
                    return new LongTensor (dims[0], dims[1]);
                case 3:
                    return new LongTensor (dims[0], dims[1], dims[2]);
                case 4:
                    return new LongTensor (dims[0], dims[1], dims[2], dims[3]);
                default:
                    throw new ArgumentOutOfRangeException (nameof (dims), "Maximum number of dimensions for tensor creation is 4.");
            }
        }
    }

    /// <summary>
    ///   Wrapper class used to surface a Aten DoubleTensor as a System.Numerics DensorTensor of double
    /// </summary>
    public sealed class DoubleAtenTensor : DenseTensor<double>, IDisposable
    {
        internal sealed class DoubleNativeMemory : MemoryManager<double>
        {
            private readonly DoubleTensor.DoubleStorage storage;

            public DoubleNativeMemory(DoubleTensor.DoubleStorage storage)
            {
                if (storage.Size < 0)
                {
                    throw new ArgumentOutOfRangeException ("Length cannot be negative.");
                }

                storage.Retain ();
                this.storage = storage;
            }

            /// <summary>
            /// Returns a span wrapping the underlying memory.
            /// Remember to Unpin the memory once the span is disposed.
            /// </summary>
            public override Span<double> GetSpan ()
            {
                ulong len = storage.Size;

                if (len > int.MaxValue)
                {
                    throw new InvalidCastException ("Tensor size not supported.");
                }

                unsafe
                {
                    return new Span<double> (storage.Data.ToPointer (), (int)len);
                }
            }

            /// <summary>
            /// Returns a handle to the memory that has been pinned and hence its address can be taken.
            /// </summary>
            /// <param name="elementIndex">The offset to the element within the memory at which the returned <see cref="MemoryHandle"/> points to. (default = 0)</param>
            public override MemoryHandle Pin (int elementIndex = 0)
            {
                if ((uint)elementIndex > storage.Size)
                {
                    throw new ArgumentOutOfRangeException (nameof (elementIndex), "Index out of array bound.");
                }

                unsafe
                {
                    storage.Retain();

                    void* pointer = Unsafe.Add<double> ((void*)storage.Data, elementIndex);
                    return new MemoryHandle (pointer, default, this);
                }
            }

            /// <summary>
            /// Lets the garbage collector know that the object is free to be moved now.
            /// </summary>
            public override void Unpin ()
            {
                storage.Free ();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    storage.Free ();
                }
            }
        }

        private readonly DoubleTensor inner;

        /// <summary>
        ///   Property returning the inner AtenSharp tensor the class is wrapping.
        /// </summary>
        public DoubleTensor AtenSharpTensor => inner;

        public DoubleAtenTensor (Memory<double> memory, ReadOnlySpan<int> dimensions, DoubleTensor inner) : base (memory, dimensions)
        {
            this.inner = inner;
        }

        /// <summary>
        ///   Utility method to create a AtenTensor.
        ///   This is currently failing if the input parameter is empty because AtenSharp.Numerics.Tensors
        ///   does not support zero-size tensors.
        /// </summary>
        /// <param name="sizes">The desired sizes for the dimensions of the tensor.</param>
        public static DoubleAtenTensor Create (params int[] sizes)
        {
            var totLength = Utils.GetTotalLength (sizes);
            var shape = sizes;

            if (sizes.Length == 0)
            {
                shape = new int[] { 0 };
            }

            var inner = CreateDoubleTensor (sizes.Select (x => (long)x).ToArray ());
            var mem = new DoubleNativeMemory (inner.Storage);

            return new DoubleAtenTensor (mem.Memory, shape, inner);
        }

        public void Dispose ()
        {
            inner.Dispose ();
        }

        /// <summary>
        /// Creates a shallow copy of this tensor, with new backing storage.
        /// </summary>
        /// <returns>A shallow copy of this tensor.</returns>
        public override Tensor<double> Clone ()
        {
            var innerClone = inner.Clone ();
            var mem = new DoubleNativeMemory (innerClone.Storage);

            return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone);
        }

        /// <summary>
        /// Creates a new Tensor of a different type with the specified dimensions and the same layout as this tensor with elements initialized to their default value.
        /// </summary>
        /// <typeparam name="TResult">Type contained in the returned Tensor.</typeparam>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor with the same layout as this tensor but different type and dimensions.</returns>
        public override Tensor<TResult> CloneEmpty<TResult> (ReadOnlySpan<int> dimensions)
        {
            switch (true)
            {
                case bool _ when typeof (TResult) == typeof (byte):
                {
                    var innerClone = ByteAtenTensor.CreateByteTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ByteAtenTensor.ByteNativeMemory (innerClone.Storage);

                    return new ByteAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (short):
                {
                    var innerClone = ShortAtenTensor.CreateShortTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ShortAtenTensor.ShortNativeMemory (innerClone.Storage);

                    return new ShortAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (int):
                {
                    var innerClone = IntAtenTensor.CreateIntTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new IntAtenTensor.IntNativeMemory (innerClone.Storage);

                    return new IntAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (long):
                {
                    var innerClone = LongAtenTensor.CreateLongTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new LongAtenTensor.LongNativeMemory (innerClone.Storage);

                    return new LongAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (double):
                {
                    var innerClone = DoubleAtenTensor.CreateDoubleTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new DoubleAtenTensor.DoubleNativeMemory (innerClone.Storage);

                    return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (float):
                {
                    var innerClone = FloatAtenTensor.CreateFloatTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new FloatAtenTensor.FloatNativeMemory (innerClone.Storage);

                    return new FloatAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                default: throw new NotImplementedException ($"Cloning type {typeof (TResult)} is not supported.");
            }
        }

        /// <summary>
        /// Reshapes the current tensor to new dimensions, using the same backing storage.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor that reinterprets backing Buffer of this tensor with different dimensions.</returns>
        public override Tensor<double> Reshape (ReadOnlySpan<int> dimensions)
        {
            if (dimensions.Length == 0)
            {
                throw new ArgumentException ("Dimensions must contain elements.", nameof (dimensions));
            }

            var newSize = Utils.GetTotalLength (dimensions);

            if (newSize != Length)
            {
                throw new ArgumentException ($"Cannot reshape array due to mismatch in lengths, currently {Length} would become {newSize}.", nameof(dimensions));
            }

            DoubleTensor reshapedTensor;

            switch (dimensions.Length)
            {
                case 1:
                    reshapedTensor = inner.NewWithStorage1d (
                        UIntPtr.Zero,
                        dimensions[0],
                        1);
                    break;
                case 2:
                    reshapedTensor = inner.NewWithStorage2d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1],
                        dimensions[1],
                        1);
                    break;
                case 3:
                    reshapedTensor = inner.NewWithStorage3d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1] + dimensions[2],
                        dimensions[1],
                        dimensions[2],
                        dimensions[2],
                        1);
                    break;
                case 4:
                    reshapedTensor = inner.NewWithStorage4d (
                        UIntPtr.Zero,
                        dimensions[0], dimensions[1] + dimensions[2] + dimensions[3],
                        dimensions[1],
                        dimensions[2] + dimensions[3],
                        dimensions[2],
                        dimensions[3],
                        dimensions[3],
                        1);
                    break;
                 default: throw new ArgumentException ($"Cannot reshape tensor with more than 4 dimensions");
            }

            return new DoubleAtenTensor (Buffer, dimensions, reshapedTensor);
        }

        /// <summary>
        ///   Creates a 1-4D tensor of the specified size(s).
        /// </summary>
        /// <param name="dims">Sizes for the dimensions.</param>
        internal static DoubleTensor CreateDoubleTensor (params long[] dims)
        {
            switch (dims.Length)
            {
                case 0:
                    return new DoubleTensor ();
                case 1:
                    return new DoubleTensor (dims[0]);
                case 2:
                    return new DoubleTensor (dims[0], dims[1]);
                case 3:
                    return new DoubleTensor (dims[0], dims[1], dims[2]);
                case 4:
                    return new DoubleTensor (dims[0], dims[1], dims[2], dims[3]);
                default:
                    throw new ArgumentOutOfRangeException (nameof (dims), "Maximum number of dimensions for tensor creation is 4.");
            }
        }
    }

    /// <summary>
    ///   Wrapper class used to surface a Aten FloatTensor as a System.Numerics DensorTensor of float
    /// </summary>
    public sealed class FloatAtenTensor : DenseTensor<float>, IDisposable
    {
        internal sealed class FloatNativeMemory : MemoryManager<float>
        {
            private readonly FloatTensor.FloatStorage storage;

            public FloatNativeMemory(FloatTensor.FloatStorage storage)
            {
                if (storage.Size < 0)
                {
                    throw new ArgumentOutOfRangeException ("Length cannot be negative.");
                }

                storage.Retain ();
                this.storage = storage;
            }

            /// <summary>
            /// Returns a span wrapping the underlying memory.
            /// Remember to Unpin the memory once the span is disposed.
            /// </summary>
            public override Span<float> GetSpan ()
            {
                ulong len = storage.Size;

                if (len > int.MaxValue)
                {
                    throw new InvalidCastException ("Tensor size not supported.");
                }

                unsafe
                {
                    return new Span<float> (storage.Data.ToPointer (), (int)len);
                }
            }

            /// <summary>
            /// Returns a handle to the memory that has been pinned and hence its address can be taken.
            /// </summary>
            /// <param name="elementIndex">The offset to the element within the memory at which the returned <see cref="MemoryHandle"/> points to. (default = 0)</param>
            public override MemoryHandle Pin (int elementIndex = 0)
            {
                if ((uint)elementIndex > storage.Size)
                {
                    throw new ArgumentOutOfRangeException (nameof (elementIndex), "Index out of array bound.");
                }

                unsafe
                {
                    storage.Retain();

                    void* pointer = Unsafe.Add<float> ((void*)storage.Data, elementIndex);
                    return new MemoryHandle (pointer, default, this);
                }
            }

            /// <summary>
            /// Lets the garbage collector know that the object is free to be moved now.
            /// </summary>
            public override void Unpin ()
            {
                storage.Free ();
            }

            protected override void Dispose (bool disposing)
            {
                if (disposing)
                {
                    storage.Free ();
                }
            }
        }

        private readonly FloatTensor inner;

        /// <summary>
        ///   Property returning the inner AtenSharp tensor the class is wrapping.
        /// </summary>
        public FloatTensor AtenSharpTensor => inner;

        public FloatAtenTensor (Memory<float> memory, ReadOnlySpan<int> dimensions, FloatTensor inner) : base (memory, dimensions)
        {
            this.inner = inner;
        }

        /// <summary>
        ///   Utility method to create a AtenTensor.
        ///   This is currently failing if the input parameter is empty because AtenSharp.Numerics.Tensors
        ///   does not support zero-size tensors.
        /// </summary>
        /// <param name="sizes">The desired sizes for the dimensions of the tensor.</param>
        public static FloatAtenTensor Create (params int[] sizes)
        {
            var totLength = Utils.GetTotalLength (sizes);
            var shape = sizes;

            if (sizes.Length == 0)
            {
                shape = new int[] { 0 };
            }

            var inner = CreateFloatTensor (sizes.Select (x => (long)x).ToArray ());
            var mem = new FloatNativeMemory (inner.Storage);

            return new FloatAtenTensor (mem.Memory, shape, inner);
        }

        public void Dispose ()
        {
            inner.Dispose ();
        }

        /// <summary>
        /// Creates a shallow copy of this tensor, with new backing storage.
        /// </summary>
        /// <returns>A shallow copy of this tensor.</returns>
        public override Tensor<float> Clone ()
        {
            var innerClone = inner.Clone ();
            var mem = new FloatNativeMemory (innerClone.Storage);

            return new FloatAtenTensor (mem.Memory, Dimensions, innerClone);
        }

        /// <summary>
        /// Creates a new Tensor of a different type with the specified dimensions and the same layout as this tensor with elements initialized to their default value.
        /// </summary>
        /// <typeparam name="TResult">Type contained in the returned Tensor.</typeparam>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor with the same layout as this tensor but different type and dimensions.</returns>
        public override Tensor<TResult> CloneEmpty<TResult> (ReadOnlySpan<int> dimensions)
        {
            switch (true)
            {
                case bool _ when typeof (TResult) == typeof (byte):
                {
                    var innerClone = ByteAtenTensor.CreateByteTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ByteAtenTensor.ByteNativeMemory (innerClone.Storage);

                    return new ByteAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (short):
                {
                    var innerClone = ShortAtenTensor.CreateShortTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new ShortAtenTensor.ShortNativeMemory (innerClone.Storage);

                    return new ShortAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (int):
                {
                    var innerClone = IntAtenTensor.CreateIntTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new IntAtenTensor.IntNativeMemory (innerClone.Storage);

                    return new IntAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (long):
                {
                    var innerClone = LongAtenTensor.CreateLongTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new LongAtenTensor.LongNativeMemory (innerClone.Storage);

                    return new LongAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (double):
                {
                    var innerClone = DoubleAtenTensor.CreateDoubleTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new DoubleAtenTensor.DoubleNativeMemory (innerClone.Storage);

                    return new DoubleAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                case bool _ when typeof (TResult) == typeof (float):
                {
                    var innerClone = FloatAtenTensor.CreateFloatTensor (inner.Shape);
                    innerClone.Fill (default);
                    var mem = new FloatAtenTensor.FloatNativeMemory (innerClone.Storage);

                    return new FloatAtenTensor (mem.Memory, Dimensions, innerClone) as Tensor<TResult>;
                }
                default: throw new NotImplementedException ($"Cloning type {typeof (TResult)} is not supported.");
            }
        }

        /// <summary>
        /// Reshapes the current tensor to new dimensions, using the same backing storage.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension of the DenseTensor to create.</param>
        /// <returns>A new tensor that reinterprets backing Buffer of this tensor with different dimensions.</returns>
        public override Tensor<float> Reshape (ReadOnlySpan<int> dimensions)
        {
            if (dimensions.Length == 0)
            {
                throw new ArgumentException ("Dimensions must contain elements.", nameof (dimensions));
            }

            var newSize = Utils.GetTotalLength (dimensions);

            if (newSize != Length)
            {
                throw new ArgumentException ($"Cannot reshape array due to mismatch in lengths, currently {Length} would become {newSize}.", nameof(dimensions));
            }

            FloatTensor reshapedTensor;

            switch (dimensions.Length)
            {
                case 1:
                    reshapedTensor = inner.NewWithStorage1d (
                        UIntPtr.Zero,
                        dimensions[0],
                        1);
                    break;
                case 2:
                    reshapedTensor = inner.NewWithStorage2d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1],
                        dimensions[1],
                        1);
                    break;
                case 3:
                    reshapedTensor = inner.NewWithStorage3d (
                        UIntPtr.Zero,
                        dimensions[0],
                        dimensions[1] + dimensions[2],
                        dimensions[1],
                        dimensions[2],
                        dimensions[2],
                        1);
                    break;
                case 4:
                    reshapedTensor = inner.NewWithStorage4d (
                        UIntPtr.Zero,
                        dimensions[0], dimensions[1] + dimensions[2] + dimensions[3],
                        dimensions[1],
                        dimensions[2] + dimensions[3],
                        dimensions[2],
                        dimensions[3],
                        dimensions[3],
                        1);
                    break;
                 default: throw new ArgumentException ($"Cannot reshape tensor with more than 4 dimensions");
            }

            return new FloatAtenTensor (Buffer, dimensions, reshapedTensor);
        }

        /// <summary>
        ///   Creates a 1-4D tensor of the specified size(s).
        /// </summary>
        /// <param name="dims">Sizes for the dimensions.</param>
        internal static FloatTensor CreateFloatTensor (params long[] dims)
        {
            switch (dims.Length)
            {
                case 0:
                    return new FloatTensor ();
                case 1:
                    return new FloatTensor (dims[0]);
                case 2:
                    return new FloatTensor (dims[0], dims[1]);
                case 3:
                    return new FloatTensor (dims[0], dims[1], dims[2]);
                case 4:
                    return new FloatTensor (dims[0], dims[1], dims[2], dims[3]);
                default:
                    throw new ArgumentOutOfRangeException (nameof (dims), "Maximum number of dimensions for tensor creation is 4.");
            }
        }
    }
}
