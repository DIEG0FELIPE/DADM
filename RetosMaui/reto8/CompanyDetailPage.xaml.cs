namespace reto8
{
    /// <summary>Muestra todos los datos de una empresa, con las opciones de editarla o eliminarla.</summary>
    public partial class CompanyDetailPage : ContentPage
    {
        private readonly CompanyRepository mRepository;
        private readonly int mCompanyId;
        private Company? mCompany;

        public CompanyDetailPage(CompanyRepository repository, int companyId)
        {
            InitializeComponent();

            mRepository = repository;
            mCompanyId = companyId;
        }

        // Al volver de editar se vuelve a leer la empresa para mostrar los cambios
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                mCompany = await mRepository.GetAsync(mCompanyId);
            }
            catch (Exception)
            {
                await DisplayAlertAsync("Error", "No se pudo leer la base de datos.", "OK");
                await Navigation.PopAsync();
                return;
            }

            if (mCompany == null)
            {
                await Navigation.PopAsync(); // ya no existe
                return;
            }

            NameLabel.Text = mCompany.Name;
            TypesLabel.Text = mCompany.ClassificationText;
            WebsiteLabel.Text = OrDash(mCompany.Website);
            PhoneLabel.Text = OrDash(mCompany.Phone);
            EmailLabel.Text = OrDash(mCompany.Email);
            ProductsLabel.Text = OrDash(mCompany.Products);
        }

        private static string OrDash(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "—" : text;
        }

        private async void OnEditClicked(object? sender, EventArgs e)
        {
            if (mCompany != null)
                await Navigation.PushAsync(new CompanyFormPage(mRepository, mCompany));
        }

        // Pide confirmación antes de borrar: la eliminación no se puede deshacer
        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            if (mCompany == null)
                return;

            bool confirmed = await DisplayAlertAsync("Eliminar empresa",
                $"¿Desea eliminar \"{mCompany.Name}\" del directorio? Esta acción no se puede deshacer.",
                "Eliminar", "Cancelar");
            if (!confirmed)
                return;

            try
            {
                await mRepository.DeleteAsync(mCompany.Id);
            }
            catch (Exception)
            {
                await DisplayAlertAsync("Error", "No se pudo eliminar la empresa.", "OK");
                return;
            }

            Toast.Show(this, "Empresa eliminada");
            await Navigation.PopAsync();
        }
    }
}
