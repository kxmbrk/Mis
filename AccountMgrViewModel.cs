using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using Playground.WpfApp.Mvvm;
using Playground.WpfApp.Repositories;
// ReSharper disable PossibleNullReferenceException
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Playground.WpfApp.Forms.DataGridsEx.AccountMgr
{
    public enum AccountActionType
    {
        Edit,
        New
    }

    public class AccountMgrViewModel : PropertyChangedBase
    {
        public override string Title => "Account Mgr: TreeView with GridView";
        private readonly IDialogCoordinator _dialogCoordinator;
        private readonly IAccountRepository _repository;
        private List<CategoryModel> _allCategories;
        private List<CategoryNode> _expandedCategoryNodes;
        private List<AccountNode> _expandedAccountNodes;

        private ObservableCollection<CategoryNode> _categoryNodes;

        public ObservableCollection<CategoryNode> CategoryNodes
        {
            get => _categoryNodes;
            set => SetPropertyValue(ref _categoryNodes, value);
        }

        private object _selectedObject;

        public object SelectedObject
        {
            get => _selectedObject;
            set => SetPropertyValue(ref _selectedObject, value);
        }

        private int _selectedTabIndex;

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetPropertyValue(ref _selectedTabIndex, value);
        }

        private List<Predicate<CategoryModel>> CategoryFilterCriteria;
        private List<Predicate<AccountModel>> AccountFilterCriteria;

        private string _accountCategorySearchText;

        public string AccountCategorySearchText
        {
            get => _accountCategorySearchText;
            set => SetPropertyValue(ref _accountCategorySearchText, value);
        }

        public AccountMgrViewModel(IDialogCoordinator dialogCoordinator)
        {
            _dialogCoordinator = dialogCoordinator;
            _repository = new AccountRepository();

            _selectedTabIndex = 0; //Setting tab value to 0 by Default so that window opens to this-Tab
            _selectedObject = null;

            _expandedCategoryNodes = new List<CategoryNode>();
            _expandedAccountNodes = new List<AccountNode>();

            CategoryFilterCriteria = new List<Predicate<CategoryModel>>();
            _categoryNameFilter = string.Empty;

            AccountFilterCriteria = new List<Predicate<AccountModel>>();
            _accountNameFilter = string.Empty;

            //Initialize commands
            CloseCommand = new DelegateCommand(() => OnClosing());
            ReloadTreeViewCommand = new DelegateCommand(() => LoadTreeView());
            ClearSearchCommand = new DelegateCommand(() => ClearSearch());
            SearchAccountCategoryCommand = new DelegateCommand(() => PerformAccountCategorySearch());

            //TreeView Context-Menu Commands
            AccountContextMenuCommand = new DelegateCommand<object>(param => AccountContextMenu_Click(param));
            CategoryContextMenuCommand = new DelegateCommand<object>(param => CategoryContextMenu_Click(param));

            //Account DataGrid-Commands
            AddNewAccountCommand = new DelegateCommand(() => AddOrEditAccount(AccountActionType.New));
            _deleteAccountCommand = new DelegateCommand(() => DeleteAccount(), () => SelectedAccount != null);
            _editAccountCommand = new DelegateCommand(() => AddOrEditAccount(AccountActionType.Edit), () => SelectedAccount != null);

            //Category DataGridCommands
            AddNewCategoryCommand = new DelegateCommand(() => AddOrEditCategory(AccountActionType.New));
            _deleteCategoryCommand = new DelegateCommand(() => DeleteCategory(), () => (SelectedCategory != null));
            _editCategoryCommand = new DelegateCommand(() => AddOrEditCategory(AccountActionType.Edit), () => (SelectedCategory != null));

            LoadData();
            LoadTreeView();

            PropertyChanged += AccountMgrViewModel_PropertyChanged;
        }

        private void LoadData()
        {
            _allCategories = _repository.GetAllCategories();

            //Load Categories for comboBox
            _categoriesComboBox = new ObservableCollection<CategoryModel>();
            foreach (var item in _allCategories)
            {
                _categoriesComboBox.Add(new CategoryModel
                {
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName
                });
            }

            CategoriesComboBox = _categoriesComboBox;
        }

        private void AccountMgrViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsDirty")
            {
                //Categories
                _deleteCategoryCommand.RaiseCanExecuteChanged();
                _editCategoryCommand.RaiseCanExecuteChanged();

                //Accounts
                _deleteAccountCommand.RaiseCanExecuteChanged();
                _editAccountCommand.RaiseCanExecuteChanged();
            }
        }

        private void LoadAccountsIntoGridView(int accountId, int parentId)
        {
            if (Accounts == null || Accounts.Count == 0)
            {
                LoadAccountsByCategoryId(parentId);
            }
            else
            {
                // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
                var existingAccount = Accounts.Where(a => a.AccountId == accountId).FirstOrDefault();

                if (existingAccount == null)
                {
                    LoadAccountsByCategoryId(parentId);
                }
            }

            SelectedAccount = Accounts.FirstOrDefault(a => a.AccountId == accountId);
        }

        private void LoadAccountsByCategoryId(int categoryId)
        {
            var accounts = _repository.GetAccountsByCategoryId(categoryId);
            Accounts = new ObservableCollection<AccountModel>();
            foreach (var item in accounts)
            {
                var acct = new AccountModel
                {
                    AccountId = item.AccountId,
                    AccountName = item.AccountName,
                    AccountLoginId = item.AccountLoginId,
                    AccountPassword = item.AccountPassword,
                    CategoryId = item.CategoryId,
                    Notes = item.Notes,
                    IsPasswordEncrypted = item.IsPasswordEncrypted
                };

                acct.PropertyChanged += AccountMgrViewModel_PropertyChanged;

                Accounts.Add(acct);
            }

            AccountsView = (CollectionView)new CollectionViewSource { Source = Accounts }.View;
            NotifyPropertyChanged("AccountsView");
            Accounts.CollectionChanged += Accounts_CollectionChanged;
        }

        private void LoadCategoriesIntoGridView(int categoryId)
        {
            if (_categories == null || _categories.Count == 0)
            {
                LoadAllCategories();

                SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == categoryId);
            }
            else
            {
                SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == categoryId);
            }
        }

        public void OnSelectedItemChanged()
        {
            if (_selectedObject != null)
            {
                //Reset accounts in the Account DataGrid.
                Accounts = null;
                NotifyPropertyChanged("Accounts");
                AccountsView = null;
                NotifyPropertyChanged("AccountsView");

                if (_selectedObject is CategoryNode categoryNode)
                {
                    LoadCategoriesIntoGridView(categoryNode.CategoryId);

                    var categoryModel = new CategoryModel
                    {
                        CategoryId = categoryNode.CategoryId,
                        CategoryName = categoryNode.CategoryName
                    };

                    SelectedCategory = categoryModel;
                    SelectedTabIndex = 1;
                }
                else if (_selectedObject is AccountNode accountNode)
                {
                    var parentNode = (CategoryNode)accountNode.Parent;
                    LoadAccountsIntoGridView(accountNode.AccountId, parentNode.CategoryId);

                    var acct = _repository.GetAccountByAccountId(accountNode.AccountId);
                    var accountModel = new AccountModel
                    {
                        AccountId = acct.AccountId,
                        AccountName = acct.AccountName,
                        AccountLoginId = acct.AccountLoginId,
                        AccountPassword = acct.AccountPassword,
                        Notes = acct.Notes,
                        CategoryId = acct.CategoryId
                    };

                    SelectedAccount = accountModel;
                    SelectedTabIndex = 2;
                }
            }
        }

        #region TreeView

        private void LoadTreeView()
        {
            if (CategoryNodes != null)
            {
                _expandedCategoryNodes.Clear();
                _expandedAccountNodes.Clear();

                foreach (var categoryNode in _categoryNodes)
                {
                    foreach (var childNode in categoryNode.Children)
                    {
                        if (childNode is AccountNode)
                        {
                            var accountNode = (AccountNode)childNode;
                            if (!_expandedAccountNodes.Contains(accountNode))
                            {
                                _expandedAccountNodes.Add(accountNode);
                            }
                        }
                    }

                    if (categoryNode.IsExpanded)
                    {
                        if (!_expandedCategoryNodes.Contains(categoryNode))
                        {
                            _expandedCategoryNodes.Add(categoryNode);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(AccountCategorySearchText))
            {
                var filteredCategories = (from a in _allCategories where a.CategoryName.ToLower().Contains(AccountCategorySearchText.ToLower()) select a).ToList();

                _categoryNodes = new ObservableCollection<CategoryNode>(
                    (from category in filteredCategories
                     select new CategoryNode(category))
                    .ToList());
            }
            else
            {
                _categoryNodes = new ObservableCollection<CategoryNode>(
                    (from category in _allCategories
                     select new CategoryNode(category))
                    .ToList());
            }

            CategoryNodes = _categoryNodes;

            if (_expandedCategoryNodes.Count > 0)
            {
                foreach (var item in CategoryNodes)
                {
                    foreach (var childItem in item.Children)
                    {
                        if (childItem is AccountNode)
                        {
                            var accountNode = (AccountNode)childItem;
                            if (DoesAccountNodeNeedToBeExpanded(accountNode))
                            {
                                childItem.IsExpanded = true;
                            }
                        }
                    }

                    if (DoesCategoryNodeNeedToBeExpanded(item))
                    {
                        item.IsExpanded = true;
                    }
                }
            }
        }

        private void LoadAllCategories()
        {
            var allCategories = _repository.GetAllCategories();
            _categories = new ObservableCollection<CategoryModel>();
            foreach (var item in allCategories)
            {
                var category = new CategoryModel();
                category.CategoryId = item.CategoryId;
                category.CategoryName = item.CategoryName;
                category.PropertyChanged += AccountMgrViewModel_PropertyChanged;

                _categories.Add(category);
            }

            CategoriesView = (CollectionView)new CollectionViewSource { Source = _categories }.View;
            NotifyPropertyChanged("CategoriesView");
            Categories.CollectionChanged += Categories_CollectionChanged;
        }

        private bool DoesCategoryNodeNeedToBeExpanded(CategoryNode categoryNode)
        {
            bool retVal = false;
            foreach (var item in _expandedCategoryNodes)
            {
                if (item.CategoryName == categoryNode.CategoryName)
                {
                    retVal = true;
                    break;
                }
            }

            return retVal;
        }

        private bool DoesAccountNodeNeedToBeExpanded(AccountNode accountNode)
        {
            bool retVal = false;
            foreach (var item in _expandedAccountNodes)
            {
                if (item.AccountName == accountNode.AccountName)
                {
                    retVal = true;
                    break;
                }
            }

            return retVal;
        }

        #endregion TreeView

        #region DataGrid: Category

        private void Categories_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    break;

                case NotifyCollectionChangedAction.Remove:
                    break;

                case NotifyCollectionChangedAction.Replace:
                    break;

                case NotifyCollectionChangedAction.Move:
                    break;

                case NotifyCollectionChangedAction.Reset:
                    break;
            }

            RefreshCategoriesView();
        }

        private ObservableCollection<CategoryModel> _categories;
        public ObservableCollection<CategoryModel> Categories => _categories;

        public CollectionView CategoriesView { get; set; }

        private CategoryModel _selectedCategory;

        public CategoryModel SelectedCategory
        {
            get => _selectedCategory;
            set => SetPropertyValue(ref _selectedCategory, value);
        }

        private string _categoryNameFilter;

        public string CategoryNameFilter
        {
            get => _categoryNameFilter;
            set
            {
                if (_categoryNameFilter == value) return;
                SetPropertyValue(ref _categoryNameFilter, value);
                ApplyCategoryFilters();
            }
        }

        private void ApplyCategoryFilters()
        {
            if (CategoriesView == null) return;

            if (string.IsNullOrEmpty(CategoryNameFilter))
            {
                RefreshCategoriesView();
                return;
            }

            try
            {
                CategoryFilterCriteria.Clear();

                if (!string.IsNullOrEmpty(CategoryNameFilter))
                {
                    CategoryFilterCriteria.Add(x => x.CategoryName.ToLower().Contains(CategoryNameFilter.ToLower()));
                }

                CategoriesView.Filter = Category_Filter;
                NotifyPropertyChanged("CategoriesView");

                RefreshCategoriesView();
            }
            catch (Exception oEx)
            {
                if (CategoriesView is IEditableCollectionView editableCollectionView)
                {
                    if (editableCollectionView.IsAddingNew)
                    {
                        editableCollectionView.CommitNew();
                    }

                    if (editableCollectionView.IsEditingItem)
                    {
                        editableCollectionView.CommitEdit();
                    }

                    CategoriesView.Filter = Category_Filter;
                    NotifyPropertyChanged("CategoriesView");
                }
                else
                {
                    _dialogCoordinator.ShowMessageAsync(this, "Exception", oEx.Message);
                }
            }
        }

        private bool Category_Filter(object item)
        {
            if (CategoryFilterCriteria.Count == 0)
                return true;
            var cat = item as CategoryModel;
            return CategoryFilterCriteria.TrueForAll(x => x(cat));
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private object RefreshCategoriesView()
        {
            try
            {
                CategoriesView.Refresh();
                NotifyPropertyChanged("CategoriesView");
            }
            catch (Exception oEx)
            {
                _dialogCoordinator.ShowMessageAsync(this, "Exception", oEx.Message);
            }
            return null;
        }

        #endregion DataGrid: Category

        #region DataGrid: Account

        private void Accounts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshAccountsView();
        }

        private ObservableCollection<AccountModel> _accounts;

        public ObservableCollection<AccountModel> Accounts
        {
            get => _accounts;
            set => SetPropertyValue(ref _accounts, value);
        }

        private ObservableCollection<CategoryModel> _categoriesComboBox;

        public ObservableCollection<CategoryModel> CategoriesComboBox
        {
            get => _categoriesComboBox;
            set => SetPropertyValue(ref _categoriesComboBox, value);
        }

        public CollectionView AccountsView { get; set; }

        private AccountModel _selectedAccount;

        public AccountModel SelectedAccount
        {
            get => _selectedAccount;
            set => SetPropertyValue(ref _selectedAccount, value);
        }

        private string _accountNameFilter;

        public string AccountNameFilter
        {
            get => _accountNameFilter;
            set
            {
                if (_accountNameFilter == value) return;
                SetPropertyValue(ref _accountNameFilter, value);
                ApplyAccountFilters();
            }
        }

        private void ApplyAccountFilters()
        {
            if (AccountsView == null) return;

            if (string.IsNullOrEmpty(AccountNameFilter))
            {
                RefreshAccountsView();
                return;
            }

            try
            {
                AccountFilterCriteria.Clear();

                if (!string.IsNullOrEmpty(AccountNameFilter))
                {
                    AccountFilterCriteria.Add(x => x.AccountName.ToLower().Contains(AccountNameFilter.ToLower()));
                }

                AccountsView.Filter = Account_Filter;
                NotifyPropertyChanged("AccountsView");

                RefreshAccountsView();
            }
            catch (Exception oEx)
            {
                if (AccountsView is IEditableCollectionView editableCollectionView)
                {
                    if (editableCollectionView.IsAddingNew)
                    {
                        editableCollectionView.CommitNew();
                    }

                    if (editableCollectionView.IsEditingItem)
                    {
                        editableCollectionView.CommitEdit();
                    }

                    AccountsView.Filter = Account_Filter;
                    NotifyPropertyChanged("AccountsView");
                }
                else
                {
                    _dialogCoordinator.ShowMessageAsync(this, "Exception", oEx.Message);
                }
            }
        }

        private bool Account_Filter(object item)
        {
            if (AccountFilterCriteria.Count == 0)
                return true;

            var acct = item as AccountModel;
            return AccountFilterCriteria.TrueForAll(x => x(acct));
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private object RefreshAccountsView()
        {
            try
            {
                AccountsView.Refresh();
                NotifyPropertyChanged("AccountsView");
            }
            catch (Exception oEx)
            {
                _dialogCoordinator.ShowMessageAsync(this, "Exception", oEx.Message);
            }
            return null;
        }

        #endregion DataGrid: Account

        #region Button/Commands

        public ICommand CloseCommand { get; }
        public ICommand ReloadTreeViewCommand { get; }
        public ICommand ClearSearchCommand { get; }

        private void ClearSearch()
        {
            AccountCategorySearchText = string.Empty;
            LoadTreeView();
        }

        public ICommand SearchAccountCategoryCommand { get; }

        private void PerformAccountCategorySearch()
        {
            if (string.IsNullOrEmpty(AccountCategorySearchText)) return;
            LoadTreeView();
        }

        public ICommand AddNewAccountCommand { get; }

        private DelegateCommand _editAccountCommand;

        public DelegateCommand EditAccountCommand
        {
            get => _editAccountCommand;
            set => _editAccountCommand = value;
        }

        private void AddOrEditAccount(AccountActionType actionType)
        {
            var accountModel = new AccountModel();

            if (actionType == AccountActionType.New)
            {
                accountModel.AccountId = -999;
                accountModel.AccountName = string.Empty;
                accountModel.AccountLoginId = string.Empty;
                accountModel.AccountPassword = string.Empty;
            }
            else if (actionType == AccountActionType.Edit)
            {
                accountModel = SelectedAccount;
            }

            var viewModel = new AccountEditorViewModel(_repository, accountModel, _allCategories, actionType);
            var view = new AccountEditorView{DataContext = viewModel};
            view.ShowDialog();

            if (viewModel.SaveSuccessful)
            {
                accountModel.PropertyChanged -= AccountMgrViewModel_PropertyChanged;
                accountModel = viewModel.Model;
                accountModel.PropertyChanged += AccountMgrViewModel_PropertyChanged;

                if (actionType == AccountActionType.New)
                {
                    if (_accounts == null)
                    {
                        _accounts = new ObservableCollection<AccountModel>();

                        AccountsView = (CollectionView)new CollectionViewSource { Source = Accounts }.View;
                        NotifyPropertyChanged("AccountsView");
                        Accounts.CollectionChanged += Accounts_CollectionChanged;
                    }
                    _accounts.Add(accountModel);
                }
                else if (actionType == AccountActionType.Edit)
                {
                    var itemToUpdate = _accounts.FirstOrDefault(a => a.AccountId  == accountModel.AccountId);
                    itemToUpdate.AccountName = accountModel.AccountName;
                    itemToUpdate.AccountLoginId = accountModel.AccountLoginId;
                    itemToUpdate.AccountPassword = accountModel.AccountPassword;
                    itemToUpdate.Notes = accountModel.Notes;
                    itemToUpdate.CategoryId = accountModel.CategoryId;
                    itemToUpdate.DateCreated = accountModel.DateCreated;
                    itemToUpdate.DateModified = accountModel.DateModified;
                }

                NotifyPropertyChanged("Accounts");
                AccountsView.Refresh();
                NotifyPropertyChanged("AccountsView");

                SelectedAccount = null;
                SelectedAccount = accountModel;
                NotifyPropertyChanged("SelectedAccount");
            }
        }

        private DelegateCommand _deleteAccountCommand;

        public DelegateCommand DeleteAccountCommand => _deleteAccountCommand;

        private void DeleteAccount()
        {
            var result = MessageBox.Show($@"Are you sure to deleted '{SelectedAccount.AccountName}'?", "Confirm Delete Account",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }

            _repository.DeleteAccount(SelectedAccount.AccountId);

            SelectedAccount.IsDeleted = true;
            SelectedAccount.IsDirty = true;
            Accounts.Remove(SelectedAccount);
            NotifyPropertyChanged("Accounts");

            _selectedAccount = null;
            NotifyPropertyChanged("SelectedAccount");

            AccountsView.Refresh();
            NotifyPropertyChanged("AccountsView");

            //Load TreeView
            LoadTreeView();
        }

        public ICommand AddNewCategoryCommand { get; }

        private void AddOrEditCategory(AccountActionType actionType)
        {
            var model = new CategoryModel();
            if (actionType == AccountActionType.New)
            {
                model.CategoryId = -999;
                model.CategoryName = string.Empty;
            }
            else if (actionType == AccountActionType.Edit)
            {
                model = SelectedCategory;
            }

            var viewModel = new CategoryEditorViewModel(_repository, model, actionType);
            var view = new CategoryEditorView {DataContext = viewModel};
            view.ShowDialog();

            if (viewModel.SaveSuccessful)
            {
                model.CategoryId = viewModel.CategoryId;
                model.CategoryName = viewModel.CategoryName;
                model.PropertyChanged += AccountMgrViewModel_PropertyChanged;

                if (actionType == AccountActionType.New)
                {
                    _categories.Add(model);
                }
                else if (actionType == AccountActionType.Edit)
                {
                    var itemToUpdate = _categories.FirstOrDefault(c => c.CategoryId == model.CategoryId);
                    itemToUpdate.CategoryName = model.CategoryName;
                }

                NotifyPropertyChanged("Categories");
                CategoriesView.Refresh();
                NotifyPropertyChanged("CategoriesView");

                SelectedCategory = null;
                SelectedCategory = model;
                NotifyPropertyChanged("SelectedCategory");

                //LoadTreeView
                LoadData();
                LoadTreeView();
            }
        }

        private DelegateCommand _deleteCategoryCommand;

        public DelegateCommand DeleteCategoryCommand
        {
            get => _deleteCategoryCommand;
            set => _deleteCategoryCommand = value;
        }

        private void DeleteCategory()
        {
            var result = MessageBox.Show($@"Deleting category will delete all accounts as well.{Environment.NewLine}Sure to delete '{SelectedCategory.CategoryName}'?", "Confirm Delete Category",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }

            _repository.DeleteAccountCategory(SelectedCategory.CategoryId);

            Categories.Remove(SelectedCategory);
            NotifyPropertyChanged("Categories");

            _selectedCategory = null;
            NotifyPropertyChanged("SelectedCategory");

            CategoriesView.Refresh();
            NotifyPropertyChanged("CategoriesView");

            //Load TreeView
            LoadData();
            LoadTreeView();

            //Load Categories
            LoadAllCategories();
        }

        private DelegateCommand _editCategoryCommand;

        public DelegateCommand EditCategoryCommand
        {
            get => _editCategoryCommand;
            set => _editCategoryCommand = value;
        }

        public ICommand AccountContextMenuCommand { get; }

        private void AccountContextMenu_Click(object param)
        {
            if (SelectedAccount != null && param != null)
            {
                if (param.ToString().ToUpper() == "VIEWDETAIL")
                {
                    var sb = new StringBuilder();

                    sb.AppendLine("Name: " + SelectedAccount.AccountName);
                    sb.AppendLine("Login: " + SelectedAccount.AccountLoginId);
                    sb.AppendLine("Password: " + SelectedAccount.AccountPassword);
                    sb.AppendLine("Notes: " + SelectedAccount.Notes);

                    MessageBox.Show(sb.ToString(), "View Details", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public ICommand CategoryContextMenuCommand { get; }

        private void CategoryContextMenu_Click(object param)
        {
            if (SelectedCategory != null && SelectedObject != null && param != null)
            {
                var categoryNode = (CategoryNode) SelectedObject;
                if (categoryNode == null) throw new ArgumentNullException(nameof(categoryNode));

                switch (param.ToString().ToUpper())
                {
                    case "DISPLAY":
                        _dialogCoordinator.ShowMessageAsync(this, "Category Context Menu: " + categoryNode.CategoryName, "Display Clicked!");
                        break;
                    case "EDIT":
                        _dialogCoordinator.ShowMessageAsync(this, "Category Context Menu: " + categoryNode.CategoryName, "Edit Clicked!");
                        break;
                    case "ADD_NEW_ACCOUNT":
                        AddOrEditAccount(AccountActionType.New);
                        break;
                }
            }
        }

        #endregion Button/Commands

        #region Closing

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                }
            }
        }


        public void OnClosing()
        {
            CloseWindow();
        }

        public override void OnWindowClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = false; //go ahead and close!
            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            _categoriesComboBox = null;
            _allCategories = null;
            _expandedCategoryNodes = null;
            _expandedAccountNodes = null;

            base.Dispose(disposing);
        }

        #endregion Closing
    }
}
