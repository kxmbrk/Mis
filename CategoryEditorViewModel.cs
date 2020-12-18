using System;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Input;
using Playground.WpfApp.Behaviors;
using Playground.WpfApp.Mvvm;
using Playground.WpfApp.Repositories;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Playground.WpfApp.Forms.DataGridsEx.AccountMgr
{
    public class CategoryEditorViewModel : ValidationPropertyChangedBase, ICloseWindow
    {
        public override string Title => _title;
        private string _title;
        private readonly IAccountRepository _repository;
        private readonly CategoryModel _backupCategoryModel;
        private readonly AccountActionType _actionType;
        private bool _promptToSave = true;
        public bool SaveSuccessful { get; set; }

        private int _categoryId;

        public int CategoryId
        {
            get => _categoryId;
            set => SetPropertyValue(ref _categoryId, value);
        }

        private string _categoryName;

        [Required(ErrorMessage = "Category Name is required!")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Category Name Should be minimum 3 characters and a maximum of 50 characters")]
        [DataType(DataType.Text)]
        public string CategoryName
        {
            get => _categoryName;
            set
            {
                SetPropertyValue(ref _categoryName, value);
                ValidateProperty(value);
            }
        }

        public CategoryEditorViewModel(IAccountRepository repository, CategoryModel model, AccountActionType actionType)
        {
            _repository = repository;
            _backupCategoryModel = model;
            _actionType = actionType;

            if (actionType == AccountActionType.New)
            {
                _title = "Create new Category";
            }
            else if (actionType == AccountActionType.New)
            {
                _title = $"Editing category: {model.CategoryId}";
            }

            _categoryId = model.CategoryId;
            _categoryName = model.CategoryName;
            ValidateProperty(_categoryName, $"CategoryName");

            NotifyPropertyChanged("Title");
            NotifyPropertyChanged("CategoryId");
            NotifyPropertyChanged("CategoryName");

            CancelCommand = new DelegateCommand(() => OnClose());
            _saveCommand = new DelegateCommand(() => SaveAndClose(), () => CanSave);

            PropertyChanged += CategoryEditorViewModel_PropertyChanged;
        }

        private void CategoryEditorViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsDirty")
            {
                _saveCommand.RaiseCanExecuteChanged();
            }
        }

        private DelegateCommand _saveCommand;

        public DelegateCommand SaveCommand
        {
            get => _saveCommand;
            set => SetPropertyValue(ref _saveCommand, value);
        }

        private bool CanSave
        {
            get
            {
                if (_categoryName != null)
                {
                    return !HasErrors && ErrorCount == 0;
                }

                return false;
            }
        }

        private void SaveAndClose()
        {
            if (_repository.IsCategoryAlreadyExist(_categoryName))
            {
                MessageBox.Show($"Category '{_categoryName}' already exist. {Environment.NewLine}Please choose a different name.", "Duplicate Category",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_actionType == AccountActionType.New)
            {
               _categoryId = _repository.InsertNewAccountCategory(_categoryName);
            }
            else if (_actionType == AccountActionType.Edit)
            {
                _repository.UpdateAccountCategory(_categoryId, _categoryName);
            }

            SaveSuccessful = true;
            OnClose();
        }

        public ICommand CancelCommand { get; }

        private void OnClose()
        {
            _promptToSave = false;
            Close?.Invoke();
        }

        public bool HasUnsavedChanges()
        {
            if (!_promptToSave) return false;

            if (_actionType == AccountActionType.New) return true;

            if (_categoryName != _backupCategoryModel.CategoryName) return true;

            return false;
        }

        //ICloseWindow implementation
        public Action Close { get; set; }

        public bool CanClose()
        {
            if (!_promptToSave) return true;

            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show("Unsaved changes found.\nDiscard changes and close?", "Confirm Close",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    return false;
                }
            }

            return true;
        }

        public void DisposeResources()
        {
            Dispose();
        }
    }
}
