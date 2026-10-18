using System.Text;
using SQLite;

namespace reto8
{
    /// <summary>Acceso a la base de datos SQLite: operaciones CRUD y búsqueda de empresas.</summary>
    public class CompanyRepository
    {
        private readonly SQLiteAsyncConnection mDb;
        private bool mInitialized = false;

        public CompanyRepository(string dbPath)
        {
            mDb = new SQLiteAsyncConnection(dbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        }

        // La tabla se crea la primera vez que se usa la base de datos
        private async Task InitAsync()
        {
            if (mInitialized)
                return;

            await mDb.CreateTableAsync<Company>();
            mInitialized = true;
        }

        // ----- Create / Update -----

        /// <summary>Inserta la empresa si es nueva (Id = 0) o actualiza la existente. Retorna su Id.</summary>
        public async Task<int> SaveAsync(Company company)
        {
            await InitAsync();

            if (company.Id == 0)
                await mDb.InsertAsync(company); // sqlite-net asigna el Id generado a la propiedad
            else
                await mDb.UpdateAsync(company);

            return company.Id;
        }

        // ----- Retrieve -----

        /// <summary>Retorna la empresa con ese Id, o null si no existe.</summary>
        public async Task<Company?> GetAsync(int id)
        {
            await InitAsync();
            return await mDb.FindAsync<Company>(id);
        }

        /// <summary>
        /// Lista las empresas ordenadas por nombre. Con un nombre, trae las que lo contienen
        /// (sin distinguir mayúsculas); con clasificaciones, las que tengan al menos una de ellas.
        /// Si se dan ambos filtros, se cumplen los dos.
        /// </summary>
        public async Task<List<Company>> SearchAsync(string? name = null, CompanyType types = CompanyType.None)
        {
            await InitAsync();

            var sql = new StringBuilder("SELECT * FROM Companies WHERE 1 = 1");
            var args = new List<object>();

            if (!string.IsNullOrWhiteSpace(name))
            {
                sql.Append(" AND Name LIKE ? ESCAPE '\\'");
                args.Add("%" + EscapeLike(name.Trim()) + "%");
            }

            if (types != CompanyType.None)
            {
                // Operador AND a nivel de bits: distinto de cero si comparten alguna clasificación
                sql.Append(" AND (Classification & ?) <> 0");
                args.Add((int)types);
            }

            sql.Append(" ORDER BY Name COLLATE NOCASE");
            return await mDb.QueryAsync<Company>(sql.ToString(), args.ToArray());
        }

        // ----- Delete -----

        public async Task DeleteAsync(int id)
        {
            await InitAsync();
            await mDb.DeleteAsync<Company>(id);
        }

        // En LIKE, % y _ son comodines; el texto buscado se toma literal
        private static string EscapeLike(string text)
        {
            return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }
    }
}
