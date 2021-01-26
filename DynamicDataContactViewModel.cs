using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using DynamicData;
using Playground.WpfApp.Behaviors;
using ReactiveUI;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Playground.WpfApp.Forms.ReactiveEx.Crud3
{
    public class DynamicDataContactViewModel : ValidatableBindableBase, ICloseWindow
    {
        public override string Title => "CRUD3 - ReactiveUI with DynamicData";

        private List<Contact> _deletedContacts;

        private SourceList<Contact> _contacts = new SourceList<Contact>();

        private readonly ReadOnlyObservableCollection<Contact> _contactsList;
        public ReadOnlyObservableCollection<Contact> Contacts => _contactsList;

        private Contact _selectedContact;

        public Contact SelectedContact
        {
            get => _selectedContact;
            set => this.RaiseAndSetIfChanged(ref _selectedContact, value);
        }

        public DynamicDataContactViewModel()
        {
            _deletedContacts = new List<Contact>();
            _contacts.AddRange(GetAllContacts());

            //Reset filter variables
            _nameFilter = string.Empty;
            _emailFilter = string.Empty;

            //using reactive ui operator to respond to any change 
            var multipleFilters = this.WhenAnyValue(
                    x => x.NameFilter,
                    x => x.EmailFilter)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Do(s =>
                {
                    Console.WriteLine($@"\r\nSearching for: {s}");
                }).Select(
                    searchTerm =>
                    {

                        var filters = BuildGroupFilter();

                        return row => filters.All(filter => filter(row));

                        bool Searcher(Contact item) => item.Name.ToLower().Contains(searchTerm.Item1.ToLower()) ||
                                                                     item.Email.ToLower().Contains(searchTerm.Item2.ToLower());

                    #pragma warning disable 162
                        return (Func<Contact, bool>)Searcher;
                        #pragma warning restore 162
                    });

            _contacts.Connect()
                .ObserveOn(RxApp.TaskpoolScheduler) // to run on background thread
                .Filter(multipleFilters)
                .ObserveOn(RxApp.MainThreadScheduler) //this needed because UI updates need to run on the main thread
                .Bind(out _contactsList)
                .DisposeMany() //Dispose TradeProxy when no longer required
                .Subscribe();

            //Add new Command
            AddNewContactCommand = ReactiveCommand.Create(() =>
            {
                _contacts.Add(new Contact {Name = string.Empty, Phone = string.Empty, Email = string.Empty, EditState = EditState.New});
            }).DisposeWith(Disposables.Value);

            //Delete Command
            var isSelected = this.WhenAnyValue(x => x.SelectedContact, (Contact c) => c != null);
            DeleteContactCommand = ReactiveCommand.Create(
                () =>
                {
                    var result = MessageBox.Show($@"Are you sure, you want to delete {SelectedContact.Name} ?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SelectedContact.EditState = EditState.Deleted;
                        SelectedContact.EndEdit();
                        _deletedContacts.Add(SelectedContact);
                        _contacts.Remove(SelectedContact);
                        SelectedContact = null;
                    }
                }, isSelected).DisposeWith(Disposables.Value);

            //Save Command
            var canExecuteSave = this.WhenAnyValue(
                x => x.SelectedContact,
                x => x.SelectedContact.Name,
                x => x.SelectedContact.Email,
                x => x.SelectedContact.Phone,
                x => x.HasErrors,
                x => x.AllErrors,
                x => x.Contacts,
                (s, n, e, p, err, errCount, cts) =>
                {
                    var isValid = true;

                    foreach (var item in cts.Where(ct => ct.EditState != EditState.NotChanged))
                    {
                        isValid = !string.IsNullOrEmpty(item.Name) && !string.IsNullOrEmpty(item.Phone) && ValidateEmail(item.Email);
                        if(!isValid) break;
                    }

                    return errCount != null && 
                           HasUnsavedChanges() && 
                           s != null && 
                           !string.IsNullOrEmpty(n) && 
                           !string.IsNullOrEmpty(e) && 
                           !string.IsNullOrEmpty(p) && 
                           !err && 
                           !errCount.Any() &&
                           isValid;
                });

            SaveCommand = ReactiveCommand.Create(() => Save(), canExecuteSave).DisposeWith(Disposables.Value);

            //Cancel/Close window Command
            CancelCommand = ReactiveCommand.Create(() =>
            {
                Close?.Invoke();
                return Unit.Default;
            });
        }

        private List<Contact> GetAllContacts()
        {
            var retVal = new List<Contact>
            {
                new Contact {Name = "Kashif", Phone = "972-207-2406", Email = "Kashif@test.com"},
                new Contact {Name = "James", Phone = "972-207-2407", Email = "James@test.com"},
                new Contact {Name = "Carlene", Phone = "972-207-2408", Email = "Carlene@test.com"}
            };

            return retVal;
        }

        public bool ValidateEmail(string email)
        {
            var emailRegExp = @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}" +
                    @"\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\" +
                    @".)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$";

            return Regex.IsMatch(email, emailRegExp);

        }

        public ReactiveCommand<Unit, Unit> AddNewContactCommand { get; }

        public ReactiveCommand<Unit, Unit> DeleteContactCommand { get; }

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        private void Save()
        {
            //Validate before saving!

            foreach (var item in _contacts.Items)
            {
                if (item.IsEditing || item.EditState == EditState.Changed)
                {
                    //Update
                    item.EditState = EditState.NotChanged;
                    item.EndEdit();
                }

                if (item.EditState == EditState.New)
                {
                    //Insert
                    item.EditState = EditState.NotChanged;
                    item.EndEdit();
                }
            }

            foreach (var item in _deletedContacts)
            {
                //Delete    
            }

            _deletedContacts.Clear();

            SelectedContact = null;
        }

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        #region Filtering
        private string _nameFilter;

        public string NameFilter
        {
            get => _nameFilter;
            set => this.RaiseAndSetIfChanged(ref _nameFilter, value);
        }

        private string _emailFilter;

        public string EmailFilter
        {
            get => _emailFilter;
            set => this.RaiseAndSetIfChanged(ref _emailFilter, value);
        }

        private IEnumerable<Predicate<Contact>> BuildGroupFilter()
        {
            if (!string.IsNullOrEmpty(NameFilter))
            {
                yield return rowView => rowView.Name.ToLower().Contains(NameFilter.ToLower());
            }

            if (!string.IsNullOrEmpty(EmailFilter))
            {
                yield return rowView => rowView.Email.ToLower().Contains(EmailFilter.ToLower());
            }
        }

        #endregion

        #region Closing
        public Action Close { get; set; }

        public bool HasUnsavedChanges()
        {
            if (_deletedContacts.Count > 0) return true;

            var isNewOrChangedObjects = Contacts
                .Where(x => x.IsEditing || x.EditState == EditState.New || x.EditState == EditState.Changed).ToList();

            if (isNewOrChangedObjects.Count > 0) return true;

            return false;
        }

        public bool CanClose()
        {
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
            _selectedContact = null;
            _contacts = null;
        }
        #endregion
    }

    public class Contact : EditableBindableBase
    {
        private string _name;

        [Display(Name = "Name")]
        [Required(ErrorMessage = "Name is required!", AllowEmptyStrings = false)]
        [StringLength(30, MinimumLength = 2, ErrorMessage = "Name Should be minimum 2 characters and a maximum of 30 characters!")]
        [DataType(DataType.Text)]
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        private string _phone;

        [Required(ErrorMessage = "Phone is required!")]
        public string Phone
        {
            get => _phone;
            set => this.RaiseAndSetIfChanged(ref _phone, value);
        }

        private string _email;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Email address is required.")]
        [RegularExpression(@"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}" +
                           @"\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\" +
                           @".)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$", ErrorMessage = "Please enter a valid email address.")]
        public string Email
        {
            get => _email;
            set => this.RaiseAndSetIfChanged(ref _email, value);
        }

    }
}
