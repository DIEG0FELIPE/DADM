namespace reto8
{
    /// <summary>Pantalla inicial: lista de empresas con filtros por nombre y clasificación.</summary>
    public partial class CompaniesPage : ContentPage
    {
        private readonly CompanyRepository mRepository;

        // Cada búsqueda lleva un número; si llega una más nueva, se descarta el resultado de la anterior
        private int mLoadVersion = 0;

        public CompaniesPage(CompanyRepository repository)
        {
            InitializeComponent();

            mRepository = repository;

            ConsultingFilter.Text = CompanyTypes.LabelOf(CompanyType.Consulting);
            CustomFilter.Text = CompanyTypes.LabelOf(CompanyType.CustomDevelopment);
            FactoryFilter.Text = CompanyTypes.LabelOf(CompanyType.SoftwareFactory);
        }

        // Al volver de agregar, editar o eliminar la lista se vuelve a leer de la base de datos
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private CompanyType SelectedTypes =>
            (ConsultingFilter.IsChecked ? CompanyType.Consulting : CompanyType.None) |
            (CustomFilter.IsChecked ? CompanyType.CustomDevelopment : CompanyType.None) |
            (FactoryFilter.IsChecked ? CompanyType.SoftwareFactory : CompanyType.None);

        private async Task LoadAsync()
        {
            int version = ++mLoadVersion;
            string name = NameSearch.Text ?? "";
            CompanyType types = SelectedTypes;

            List<Company> companies;
            try
            {
                companies = await mRepository.SearchAsync(name, types);
            }
            catch (Exception)
            {
                await DisplayAlertAsync("Error", "No se pudo leer la base de datos.", "OK");
                return;
            }

            if (version != mLoadVersion)
                return;

            bool filtering = !string.IsNullOrWhiteSpace(name) || types != CompanyType.None;

            CompaniesList.ItemsSource = companies;
            CountLabel.Text = companies.Count == 1 ? "1 empresa" : $"{companies.Count} empresas";
            EmptyLabel.Text = filtering
                ? "No hay empresas que coincidan con el filtro."
                : "Aún no hay empresas en el directorio.\nToca + para agregar la primera.";
            EmptyLabel.IsVisible = companies.Count == 0;
        }

        private async void OnFilterChanged(object? sender, EventArgs e)
        {
            await LoadAsync();
        }

        private async void OnCompanyTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is Company company)
                await Navigation.PushAsync(new CompanyDetailPage(mRepository, company.Id));
        }

        private async void OnAddClicked(object? sender, EventArgs e)
        {
            await Navigation.PushAsync(new CompanyFormPage(mRepository, null));
        }
    }
}
