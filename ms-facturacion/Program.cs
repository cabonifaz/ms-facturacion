using ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;
using ms_facturacion.Aplicacion.CasosDeUso.Empresas;
using ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;
using ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Infraestructura.Persistencia;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Puertos → Adaptadores (Infraestructura)
builder.Services.AddScoped<IInquilinoRepositorio, InquilinoRepositorioSql>();
builder.Services.AddScoped<IEmpresaRepositorio, EmpresaRepositorioSql>();
builder.Services.AddScoped<ISerieDocumentoRepositorio, SerieDocumentoRepositorioSql>();
builder.Services.AddScoped<IDocumentoElectronicoRepositorio, DocumentoElectronicoRepositorioSql>();

// Casos de Uso — Inquilino
builder.Services.AddScoped<InsertarInquilinoCasoDeUso>();
builder.Services.AddScoped<ObtenerInquilinoCasoDeUso>();
builder.Services.AddScoped<ListarInquilinosCasoDeUso>();
builder.Services.AddScoped<ActualizarInquilinoCasoDeUso>();
builder.Services.AddScoped<EliminarInquilinoCasoDeUso>();

// Casos de Uso — Empresa
builder.Services.AddScoped<InsertarEmpresaCasoDeUso>();
builder.Services.AddScoped<ObtenerEmpresaCasoDeUso>();
builder.Services.AddScoped<ListarEmpresasCasoDeUso>();
builder.Services.AddScoped<ActualizarEmpresaCasoDeUso>();
builder.Services.AddScoped<EliminarEmpresaCasoDeUso>();

// Casos de Uso — SerieDocumento
builder.Services.AddScoped<InsertarSerieDocumentoCasoDeUso>();
builder.Services.AddScoped<ObtenerSerieDocumentoCasoDeUso>();
builder.Services.AddScoped<ListarSeriesDocumentoCasoDeUso>();
builder.Services.AddScoped<ActualizarSerieDocumentoCasoDeUso>();
builder.Services.AddScoped<EliminarSerieDocumentoCasoDeUso>();

// Casos de Uso — DocumentoElectronico
builder.Services.AddScoped<InsertarDocumentoElectronicoCasoDeUso>();
builder.Services.AddScoped<ObtenerDocumentoElectronicoCasoDeUso>();
builder.Services.AddScoped<ListarDocumentosElectronicosCasoDeUso>();
builder.Services.AddScoped<ActualizarEstadoSunatDocumentoElectronicoCasoDeUso>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ms_facturacion API");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
