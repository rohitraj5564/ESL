using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadleCompare : IComparer<ESLLadle>
    {
        public enum SortBy
        {
            TimeSpent
        }
        private SortBy compareField = SortBy.TimeSpent;
        public int Compare(ESLLadle x, ESLLadle y)
        {
            switch (compareField)
            {
                case SortBy.TimeSpent:
                    if (x.TimeSpentObj.TotalMinutes < y.TimeSpentObj.TotalMinutes)
                        return -1;
                    break;
                default:
                    break;
            }
            return x.TimeSpentObj.CompareTo(y.TimeSpentObj);
        }
    }
}
