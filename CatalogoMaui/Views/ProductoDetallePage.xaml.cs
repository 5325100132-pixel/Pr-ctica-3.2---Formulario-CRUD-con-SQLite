using System.Globalization;
using CatalogoMaui.Models;
using CatalogoMaui.Services;

namespace CatalogoMaui.Views;

public partial class ProductoDetallePage : ContentPage, IQueryAttributable
{
    private int id;
    private string fotoActual = "mochila.jpg";
    private string? rutaTemporalSeleccionada;

    public ProductoDetallePage()
    {
        InitializeComponent();
        Title = "Agregar producto";
        Foto.ItemsSource = new List<string> { "mochila.jpg", "libreta.jpg", "termo.jpg" };
        Foto.SelectedIndex = 0;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("producto", out var dato) || dato is not Producto p)
            return;

        id = p.Id;
        fotoActual = p.Foto;
        Title = "Editar producto";
        Nombre.Text = p.Nombre;
        Descripcion.Text = p.Descripcion;
        Detalle.Text = p.Detalle;
        Precio.Text = p.Precio.ToString("0.00", CultureInfo.CurrentCulture);

        if (FotosProducto.EsIncluida(p.Foto))
        {
            Foto.SelectedItem = p.Foto;
        }
        else
        {
            Foto.SelectedIndex = -1;
            VistaFoto.Source = FotosProducto.Resolver(p.Foto);
        }

        Eliminar.IsVisible = true;
    }

    private void AlCambiarFoto(object? sender, EventArgs e)
    {
        if (Foto.SelectedItem is string archivo)
        {
            rutaTemporalSeleccionada = null;
            fotoActual = archivo;
            VistaFoto.Source = archivo;
        }
    }

    private async void AlElegirFoto(object? sender, EventArgs e)
    {
        try
        {
            var rutaTemp = await FotosProducto.ElegirAsync();
            if (rutaTemp is null) return;

            rutaTemporalSeleccionada = rutaTemp;
            Foto.SelectedIndex = -1;
            VistaFoto.Source = ImageSource.FromFile(rutaTemp);
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo cargar la foto", ex.Message, "Aceptar");
        }
    }

    private async void AlGuardar(object? sender, EventArgs e)
    {
        if (!Guardar.IsEnabled) return;

        var textoPrecio = Precio.Text?.Trim().Replace(',', '.');
        if (string.IsNullOrWhiteSpace(Nombre.Text) ||
            string.IsNullOrWhiteSpace(Descripcion.Text) ||
            string.IsNullOrWhiteSpace(Detalle.Text) ||
            !decimal.TryParse(textoPrecio, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var importe) ||
            importe < 0 || importe > 99999999.99m ||
            decimal.Round(importe, 2) != importe)
        {
            await DisplayAlert("Revisa los datos",
                "Completa los campos y escribe un precio válido de hasta dos decimales.", "Aceptar");
            return;
        }

        await EjecutarAsync(async () =>
        {
            string fotoAnterior = fotoActual;
            string fotoNueva = fotoActual;
            bool copioNueva = false;

            if (!string.IsNullOrWhiteSpace(rutaTemporalSeleccionada))
            {
                fotoNueva = await FotosProducto.GuardarAsync(rutaTemporalSeleccionada);
                copioNueva = true;
            }
            else if (Foto.SelectedItem is string incluida)
            {
                fotoNueva = incluida;
            }

            var producto = new Producto
            {
                Id = id,
                Nombre = Nombre.Text.Trim(),
                Descripcion = Descripcion.Text.Trim(),
                Detalle = Detalle.Text.Trim(),
                Precio = importe,
                Foto = fotoNueva
            };

            try
            {
                if (id == 0)
                    await ProductosSqlite.Instancia.Insertar(producto);
                else if (!await ProductosSqlite.Instancia.Actualizar(producto))
                    throw new InvalidOperationException("El producto ya no existe.");

                // Si se guardó bien y cambió la foto, eliminamos la foto propia anterior
                if (fotoAnterior != fotoNueva)
                    FotosProducto.EliminarSiEsPropia(fotoAnterior);

                return true;
            }
            catch
            {
                // Si falla SQLite, borramos la foto recién copiada para no dejar basura
                if (copioNueva)
                    FotosProducto.EliminarSiEsPropia(fotoNueva);
                throw;
            }
        });
    }

    private async void AlEliminar(object? sender, EventArgs e)
    {
        if (!Eliminar.IsEnabled || id == 0) return;

        await EjecutarAsync(async () =>
        {
            if (!await DisplayAlert("Eliminar producto", "¿Deseas eliminar este producto?",
                "Eliminar", "Cancelar"))
                return false;

            if (!await ProductosSqlite.Instancia.Eliminar(id))
                throw new InvalidOperationException("El producto ya no existe.");

            FotosProducto.EliminarSiEsPropia(fotoActual);
            return true;
        });
    }

    private async Task EjecutarAsync(Func<Task<bool>> operacion)
    {
        Guardar.IsEnabled = Eliminar.IsEnabled = false;
        try
        {
            if (await operacion())
                await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se completó la operación", ex.Message, "Aceptar");
        }
        finally
        {
            Guardar.IsEnabled = Eliminar.IsEnabled = true;
        }
    }
}