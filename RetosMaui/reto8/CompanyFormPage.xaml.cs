using System.Net.Mail;
using System.Text.RegularExpressions;

namespace reto8
{
    /// <summary>Formulario para crear una empresa nueva o editar una existente.</summary>
    public partial class CompanyFormPage : ContentPage
    {
        private readonly CompanyRepository mRepository;

        // Empresa que se edita; null cuando se crea una nueva
        private readonly Company? mCompany;

        // Dígitos y los signos habituales de un teléfono (+57 (1) 234-5678)
        private static readonly Regex PhonePattern = new(@"^\+?[0-9 ()\-.]{7,20}$");

        public CompanyFormPage(CompanyRepository repository, Company? company)
        {
            InitializeComponent();

            mRepository = repository;
            mCompany = company;

            ConsultingOption.Text = CompanyTypes.LabelOf(CompanyType.Consulting);
            CustomOption.Text = CompanyTypes.LabelOf(CompanyType.CustomDevelopment);
            FactoryOption.Text = CompanyTypes.LabelOf(CompanyType.SoftwareFactory);

            if (company == null)
            {
                Title = "Nueva empresa";
                return;
            }

            // Edición: mostrar los datos actuales
            Title = "Editar empresa";
            NameEntry.Text = company.Name;
            WebsiteEntry.Text = company.Website;
            PhoneEntry.Text = company.Phone;
            EmailEntry.Text = company.Email;
            ProductsEditor.Text = company.Products;
            ConsultingOption.IsChecked = company.Classification.HasFlag(CompanyType.Consulting);
            CustomOption.IsChecked = company.Classification.HasFlag(CompanyType.CustomDevelopment);
            FactoryOption.IsChecked = company.Classification.HasFlag(CompanyType.SoftwareFactory);
        }

        private CompanyType SelectedTypes =>
            (ConsultingOption.IsChecked ? CompanyType.Consulting : CompanyType.None) |
            (CustomOption.IsChecked ? CompanyType.CustomDevelopment : CompanyType.None) |
            (FactoryOption.IsChecked ? CompanyType.SoftwareFactory : CompanyType.None);

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            string name = (NameEntry.Text ?? "").Trim();
            string website = (WebsiteEntry.Text ?? "").Trim();
            string phone = (PhoneEntry.Text ?? "").Trim();
            string email = (EmailEntry.Text ?? "").Trim();

            string? error = Validate(name, ref website, phone, email, SelectedTypes);
            if (error != null)
            {
                ErrorLabel.Text = error;
                ErrorLabel.IsVisible = true;
                return;
            }

            ErrorLabel.IsVisible = false;
            SaveButton.IsEnabled = false;

            // Al editar se conserva el Id, para que se actualice la fila en vez de crear otra
            var company = new Company
            {
                Id = mCompany?.Id ?? 0,
                Name = name,
                Website = website,
                Phone = phone,
                Email = email,
                Products = (ProductsEditor.Text ?? "").Trim(),
                Classification = SelectedTypes,
            };

            try
            {
                await mRepository.SaveAsync(company);
            }
            catch (Exception)
            {
                SaveButton.IsEnabled = true;
                await DisplayAlertAsync("Error", "No se pudo guardar la empresa.", "OK");
                return;
            }

            Toast.Show(this, mCompany == null ? "Empresa agregada" : "Cambios guardados");
            await Navigation.PopAsync();
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        /// <summary>Retorna el mensaje del primer problema encontrado, o null si todo es válido.</summary>
        private static string? Validate(string name, ref string website, string phone, string email, CompanyType types)
        {
            if (name.Length == 0)
                return "Ingrese el nombre de la empresa.";

            if (website.Length > 0)
            {
                // Si falta el protocolo se asume https
                if (!website.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    website = "https://" + website;

                if (!Uri.TryCreate(website, UriKind.Absolute, out Uri? uri) || !uri.Host.Contains('.'))
                    return "La página web no es válida.";
            }

            if (phone.Length > 0 && !PhonePattern.IsMatch(phone))
                return "El teléfono no es válido.";

            if (email.Length > 0 && !IsValidEmail(email))
                return "El correo electrónico no es válido.";

            if (types == CompanyType.None)
                return "Seleccione al menos una clasificación.";

            return null;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);
                return address.Address == email && address.Host.Contains('.');
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
