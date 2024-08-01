﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class Statistics
    {
        public static IEnumerable<double> FilterOutliers(List<double> times)
        {
            if (times == null || times.Count < 4)
            {
                throw new ArgumentException("The list must contain at least 4 elements.");
            }

            // Sort the list
            var sortedTimes = times.OrderBy(t => t).ToList();

            // Calculate the quartiles
            double Q1 = GetPercentile(sortedTimes, 25);
            double Q3 = GetPercentile(sortedTimes, 75);

            // Calculate the interquartile range (IQR)
            double IQR = Q3 - Q1;

            // Define the acceptable range for non-outliers
            double lowerBound = Q1 - 1.5 * IQR;
            double upperBound = Q3 + 1.5 * IQR;

            // Filter out the outliers
            var filteredTimes = sortedTimes.Where(t => t >= lowerBound && t <= upperBound);

            return filteredTimes;
        }

        private static double GetPercentile(List<double> sortedList, double percentile)
        {
            int N = sortedList.Count;
            double rank = (percentile / 100.0) * (N - 1);
            int lowIndex = (int)Math.Floor(rank);
            int highIndex = (int)Math.Ceiling(rank);
            double fraction = rank - lowIndex;

            if (highIndex >= N) return sortedList[lowIndex];
            return sortedList[lowIndex] + fraction * (sortedList[highIndex] - sortedList[lowIndex]);
        }
    }
}
