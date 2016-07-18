using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Runtime.InteropServices;

namespace Conf
{
    /// <summary>
    /// Helper class to detect the MIME type based on the file header signature.
    /// </summary>
    public static class MimeSniffer
    {
        /// <summary>
        /// Internet Explorer 9. Returns image/png and image/jpeg instead of 
        /// image/x-png and image/pjpeg.
        /// </summary>
        private const uint FMFD_RETURNUPDATEDIMGMIMES = 0x20;

        /// <summary>
        /// The zero (0) value for Reserved parameters.
        /// </summary>
        private const uint RESERVED = 0;

        /// <summary>
        /// The value that is returned when the MIME type cannot be recognized.
        /// </summary>
        private const string UNKNOWN = "unknown/unknown";

        /// <summary>
        /// The return value which indicates that the operation completed successfully.
        /// </summary>
        private const uint S_OK = 0;


        /// <summary>
        /// Determines the MIME type from the data provided.
        /// </summary>
        /// <param name="pBC">A pointer to the IBindCtx interface. Can be set to NULL.</param>
        /// <param name="pwzUrl">A pointer to a string value that contains the URL of the data. Can be set to NULL if <paramref name="pBuffer"/> contains the data to be sniffed.</param>
        /// <param name="pBuffer">A pointer to the buffer that contains the data to be sniffed. Can be set to NULL if <paramref name="pwzUrl"/> contains a valid URL.</param>
        /// <param name="cbSize">An unsigned long integer value that contains the size of the buffer.</param>
        /// <param name="pwzMimeProposed">A pointer to a string value that contains the proposed MIME type. This value is authoritative if type cannot be determined from the data. If the proposed type contains a semi-colon (;) it is removed. This parameter can be set to NULL.</param>
        /// <param name="dwMimeFlags">The flags which modifies the behavior of the function.</param>
        /// <param name="ppwzMimeOut">The address of a string value that receives the suggested MIME type.</param>
        /// <param name="dwReserverd">Reserved. Must be set to 0.</param>
        /// <returns>S_OK, E_FAIL, E_INVALIDARG or E_OUTOFMEMORY.</returns>
        /// <remarks>
        /// Read more: http://msdn.microsoft.com/en-us/library/ms775107(v=vs.85).aspx
        /// </remarks>
        [DllImport(@"urlmon.dll", CharSet = CharSet.Auto)]
        private extern static uint FindMimeFromData(
                uint pBC,
                [MarshalAs(UnmanagedType.LPStr)] string pwzUrl,
                [MarshalAs(UnmanagedType.LPArray)] byte[] pBuffer,
                uint cbSize,
                [MarshalAs(UnmanagedType.LPStr)] string pwzMimeProposed,
                uint dwMimeFlags,
                out uint ppwzMimeOut,
                uint dwReserverd
        );


        /// <summary>
        /// Returns the MIME type for the specified file header.
        /// </summary>
        /// <param name="header">The header to examine.</param>
        /// <returns>The MIME type or "unknown/unknown" if the type cannot be recognized.</returns>
        /// <remarks>
        /// NOTE: This method recognizes only 26 types used by IE.
        /// http://msdn.microsoft.com/en-us/library/ms775147(VS.85).aspx#Known_MimeTypes
        /// </remarks>
        public static string GetMime(byte[] header)
        {
            try
            {
                uint mimetype;
                uint result = FindMimeFromData(0,
                                                null,
                                                header,
                                                (uint)header.Length,
                                                null,
                                                FMFD_RETURNUPDATEDIMGMIMES,
                                                out mimetype,
                                                RESERVED);
                if (result != S_OK)
                {
                    return UNKNOWN;
                }

                IntPtr mimeTypePtr = new IntPtr(mimetype);
                string mime = Marshal.PtrToStringUni(mimeTypePtr);
                Marshal.FreeCoTaskMem(mimeTypePtr);
                return mime;
            }
            catch
            {
                return UNKNOWN;
            }
        }

    }
}
