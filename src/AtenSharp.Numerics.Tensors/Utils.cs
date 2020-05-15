﻿using System;

namespace AtenSharp.Numerics.Tensors
{
    internal class Utils
    {
        /// <summary>
        ///   Computes the total size of a tensor starting from some inpit dimension.
        /// </summary>
        /// <param name="dimensions">An span of integers that represent the size of each dimension.</param>
        /// <param name="startIndex">The index of the first dimension to consider.</param>
        /// <returns>The total size of tensor.</returns>
        public static int GetTotalLength (ReadOnlySpan<int> dimensions, int startIndex = 0)
        {
            if (dimensions.Length == 0)
            {
                return 0;
            }

            int product = 1;
            for (int i = startIndex; i < dimensions.Length; i++)
            {
                if (dimensions[i] < 0)
                {
                    throw new ArgumentOutOfRangeException ($"{nameof (dimensions)}[{i}]");
                }

                checked
                {
                    product *= dimensions[i];
                }
            }

            return product;
        }
    }
}
