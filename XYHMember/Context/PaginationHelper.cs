using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace XYHMember.Context
{

    public static class PaginationHelper
    {
        public static List<T> GetPaged<T>(List<T> sourceData, int page, int pageSize)
        {
            int skip = (page - 1) * pageSize;
            return sourceData.Skip(skip).Take(pageSize).ToList();
        }
    }

}