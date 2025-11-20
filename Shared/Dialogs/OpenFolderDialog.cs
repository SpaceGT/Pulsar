using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Pulsar.Shared.Dialogs;

/// <summary>
/// Windows Vista folder dialog.
/// </summary>
public class OpenFolderDialog : CommonDialog, IDisposable
{
    class VistaFileOpenDialog : IDisposable
    {
        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        class FileOpenDialog { }

        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileOpenDialog
        {
            [PreserveSig]
            uint Show(nint hwndOwner);
            void SetFileTypes();        // notimpl
            void SetFileTypeIndex();    // notimpl
            void GetFileTypeIndex();    // notimpl
            void Advise();              // notimpl
            void Unadvise();            // notimpl
            void SetOptions(FILEOPENDIALOGOPTIONS fos);
            void GetOptions(out FILEOPENDIALOGOPTIONS pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName(string pszName);
            void GetFileName(out string pszName);
            void SetTitle(string pszTitle);
            void SetOkButtonLabel();    // notimpl
            void SetFileNameLabel();    // notimpl
            void GetResult(out IShellItem ppsi);
            void AddPlace();            // notimpl
            void SetDefaultExtension(string pszDefaultExtension);
            void Close(uint hr);
            void SetClientGuid();       // notimpl
            void ClearClientData();
            void SetFilter();           // notimpl
            void GetResults();          // notimpl
            void GetSelectedItems();    // notimpl
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler();    // notimpl
            void GetParent();        // notimpl
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes();    // notimpl
            void Compare();          // notimpl
        }

        /// <summary>
        /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-sigdn
        /// </summary>
        enum SIGDN : uint
        {
            SIGDN_NORMALDISPLAY               = 0,
            SIGDN_PARENTRELATIVEPARSING       = 0x80018001,
            SIGDN_DESKTOPABSOLUTEPARSING      = 0x80028000,
            SIGDN_PARENTRELATIVEEDITING       = 0x80031001,
            SIGDN_DESKTOPABSOLUTEEDITING      = 0x8004c000,
            SIGDN_FILESYSPATH                 = 0x80058000,
            SIGDN_URL                         = 0x80068000,
            SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            SIGDN_PARENTRELATIVE              = 0x80080001,
            SIGDN_PARENTRELATIVEFORUI         = 0x80094001,
        }

        private FileOpenDialog _dialog;
        private IFileOpenDialog _interface;

        public VistaFileOpenDialog()
        {
            _dialog = new FileOpenDialog();
            _interface = (IFileOpenDialog)_dialog;
        }

        public void SetOptions(FILEOPENDIALOGOPTIONS options) => _interface.SetOptions(options);
        public void SetTitle(string title) => _interface.SetTitle(title);

        public bool Show(nint hwndOwner)
        {
            const uint HR_S_OK = 0;
            return _interface.Show(hwndOwner) == HR_S_OK;
        }

        public string GetResult()
        {
            _interface.GetResult(out IShellItem item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string fileName);
            return fileName;
        }

        public void Dispose()
        {
            try
            {
                if (_dialog is not null)
                    Marshal.ReleaseComObject(_dialog);
                if (_interface is not null)
                    Marshal.ReleaseComObject(_interface);
            }
            finally
            {
                _dialog = null;
                _interface = null;
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_fileopendialogoptions
    /// </summary>
    [Flags]
    enum FILEOPENDIALOGOPTIONS : uint
    {
        FOS_OVERWRITEPROMPT = 0x00000002,
        FOS_STRICTFILETYPES = 0x00000004,
        FOS_NOCHANGEDIR = 0x00000008,
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_ALLNONSTORAGEITEMS = 0x00000080,
        FOS_NOVALIDATE = 0x00000100,
        FOS_ALLOWMULTISELECT = 0x00000200,
        FOS_PATHMUSTEXIST = 0x00000800,
        FOS_FILEMUSTEXIST = 0x00001000,
        FOS_CREATEPROMPT = 0x00002000,
        FOS_SHAREAWARE = 0x00004000,
        FOS_NOREADONLYRETURN = 0x00008000,
        FOS_NOTESTFILECREATE = 0x00010000,
        FOS_HIDEMRUPLACES = 0x00020000,
        FOS_HIDEPINNEDPLACES = 0x00040000,
        FOS_NODEREFERENCELINKS = 0x00100000,
        FOS_OKBUTTONNEEDSINTERACTION = 0x00200000,
        FOS_DONTADDTORECENT = 0x02000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000,
        FOS_FORCEPREVIEWPANEON = 0x40000000,
        FOS_SUPPORTSTREAMABLEITEMS = 0x80000000,
    }

    /// <summary>
    /// Gets or sets the folder dialog box title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dialog box displays a warning if the user specifies a path that does not exist.
    /// </summary>
    public bool CheckPathExists { get; set; }

    /// <summary>
    /// Gets or sets a string containing the folder name selected in the folder dialog box.
    /// </summary>
    public string FolderName { get; private set; }

    public OpenFolderDialog()
    {
        Reset();
    }

    protected override bool RunDialog(nint hwndOwner)
    {
        if (Control.CheckForIllegalCrossThreadCalls && Application.OleRequired() != ApartmentState.STA)
        {
            throw new ThreadStateException("Current thread must be set to STA mode.");
        }

        using VistaFileOpenDialog dialog = new();
        dialog.SetTitle(Title);
        dialog.SetOptions(GetOptions());

        bool success = dialog.Show(hwndOwner);
        if (success)
        {
            FolderName = dialog.GetResult();
            return true;
        }
        return false;
    }

    private FILEOPENDIALOGOPTIONS GetOptions()
    {
        var options = FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM;
        if (CheckPathExists)
        {
            options |= FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
        }
        return options;
    }

    public override void Reset()
    {
        Title = null;
        CheckPathExists = true;
        FolderName = null;
    }
}
