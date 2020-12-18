using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Playground.WpfApp.Mvvm;

namespace Playground.WpfApp.Forms.DataGridsEx.AccountMgr
{
    public class AccountModel : ValidationPropertyChangedBase, IEditableObject
    {
        private int _accountId;

        public int AccountId
        {
            get => _accountId;
            set => SetPropertyValue(ref _accountId, value);
        }

        private string _accountName;

        [Required(ErrorMessage = "Account Name is required!")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Account Name Should be minimum 3 characters and a maximum of 50 characters")]
        [DataType(DataType.Text)]
        public string AccountName
        {
            get => _accountName;
            set
            {
                SetPropertyValue(ref _accountName, value);
                ValidateProperty(value);
            }
        }

        private string _accountLoginId;

        [Required(ErrorMessage = "Account LoginId is required!")]
        public string AccountLoginId
        {
            get => _accountLoginId;
            set
            {
                SetPropertyValue(ref _accountLoginId, value);
                ValidateProperty(value);
            }
        }

        private string _accountPassword;

        [Required(ErrorMessage = "Account Password is required!")]
        public string AccountPassword
        {
            get => _accountPassword;
            set
            {
                SetPropertyValue(ref _accountPassword, value);
                ValidateProperty(value);
            }
        }

        private string _notes;

        public string Notes
        {
            get => _notes;
            set => SetPropertyValue(ref _notes, value);
        }

        private DateTime? _dateCreated;

        public DateTime? DateCreated
        {
            get => _dateCreated;
            set => SetPropertyValue(ref _dateCreated, value);
        }

        private DateTime? _dateModified;

        public DateTime? DateModified
        {
            get => _dateModified;
            set => SetPropertyValue(ref _dateModified, value);
        }

        private int _categoryId;

        [Required(ErrorMessage = "Category is required")]
        public int CategoryId
        {
            get => _categoryId;
            set => SetPropertyValue(ref _categoryId, value);
        }

        private string _isPasswordEncrypted;

        public string IsPasswordEncrypted
        {
            get => _isPasswordEncrypted;
            set => SetPropertyValue(ref _isPasswordEncrypted, value);
        }

        #region IEditableObject implementation

        private AccountModel _backupCopy;
        private bool _inEdit;

        public void BeginEdit()
        {
            if (_inEdit) return;
            _inEdit = true;
            _backupCopy = MemberwiseClone() as AccountModel;
            IsDirty = true;
        }

        public void CancelEdit()
        {
            if (!_inEdit) return;
            _inEdit = false;
            AccountId = _backupCopy.AccountId;
            AccountName = _backupCopy.AccountName;
            AccountLoginId = _backupCopy.AccountLoginId;
            AccountPassword = _backupCopy.AccountPassword;
            Notes = _backupCopy.Notes;
            DateCreated = _backupCopy.DateCreated;
            DateModified = _backupCopy.DateModified;
            CategoryId = _backupCopy.CategoryId;
            IsPasswordEncrypted = _backupCopy.IsPasswordEncrypted;
        }

        public void EndEdit()
        {
            if (!_inEdit) return;
            _inEdit = false;
            _backupCopy = null;
        }

        #endregion IEditableObject implementation
    }
}
