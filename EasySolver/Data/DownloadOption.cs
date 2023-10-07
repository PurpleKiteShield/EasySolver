using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasySolver.Data
{
    [Flags]
    public enum DownloadOption : byte
    {
        Local = 0,
        Download = 1 << 0,
        MustDownload = Download | 1 << 1,
        ShowList = 1 << 2,
    }
}
