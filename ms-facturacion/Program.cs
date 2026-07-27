using ms_facturacion.Aplicacion.CasosDeUso.Certificados;
using ms_facturacion.Aplicacion.CasosDeUso.Clientes;
using ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;
using ms_facturacion.Aplicacion.CasosDeUso.Credenciales;
using ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;
using ms_facturacion.Aplicacion.CasosDeUso.Empresas;
using ms_facturacion.Aplicacion.CasosDeUso.Inquilinos;
using ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;
using ms_facturacion.Aplicacion.CasosDeUso.SeriesDocumento;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Infraestructura.Almacenamiento;
using ms_facturacion.Infraestructura.Cifrado;
using ms_facturacion.Infraestructura.Persistencia;
using ms_facturacion.Infraestructura.Sunat;
using ms_facturacion.Infraestructura.Xml;
using Amazon.Runtime;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// AWS S3 — mismo patrón de conexión que maximlian3_backend/SafetyReport.WebApi/Program.cs:
// credenciales estáticas (AWS:AccessKey/AWS:SecretKey) + región (AWS:Region), un solo IAmazonS3 singleton.
var awsRegion = builder.Configuration["AWS:Region"]
    ?? throw new InvalidOperationException("No se configuró 'AWS:Region'.");
var awsAccessKey = builder.Configuration["AWS:AccessKey"]
    ?? throw new InvalidOperationException("No se configuró 'AWS:AccessKey'.");
var awsSecretKey = builder.Configuration["AWS:SecretKey"]
    ?? throw new InvalidOperationException("No se configuró 'AWS:SecretKey'.");

builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
    var credenciales = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
    return new AmazonS3Client(credenciales, regionEndpoint);
});

// Puertos → Adaptadores (Infraestructura)
builder.Services.AddScoped<IInquilinoRepositorio, InquilinoRepositorioSql>();
builder.Services.AddScoped<IEmpresaRepositorio, EmpresaRepositorioSql>();
builder.Services.AddScoped<IClienteRepositorio, ClienteRepositorioSql>();
builder.Services.AddScoped<ISerieDocumentoRepositorio, SerieDocumentoRepositorioSql>();
builder.Services.AddScoped<IDocumentoElectronicoRepositorio, DocumentoElectronicoRepositorioSql>();
builder.Services.AddScoped<ICertificadoRepositorio, CertificadoRepositorioSql>();
builder.Services.AddScoped<ICredencialInquilinoRepositorio, CredencialInquilinoRepositorioSql>();
builder.Services.AddScoped<IConfiguracionFacturacionEmpresaRepositorio, ConfiguracionFacturacionEmpresaRepositorioSql>();
builder.Services.AddScoped<ICifradoInquilinoServicio, CifradoInquilinoServicioAesGcm>();
builder.Services.AddScoped<IArchivoDocumentoRepositorio, ArchivoDocumentoRepositorioSql>();
builder.Services.AddScoped<ITransmisionSunatRepositorio, TransmisionSunatRepositorioSql>();
builder.Services.AddScoped<IErrorDocumentoRepositorio, ErrorDocumentoRepositorioSql>();
builder.Services.AddScoped<ILoteDocumentoRepositorio, LoteDocumentoRepositorioSql>();
builder.Services.AddScoped<IItemLoteDocumentoRepositorio, ItemLoteDocumentoRepositorioSql>();

// Módulo 4 — Worker (construir/firmar/empaquetar/enviar a SUNAT)
builder.Services.AddScoped<IConstructorXmlComprobanteServicio, ConstructorXmlComprobanteServicio>();
builder.Services.AddScoped<IConstructorXmlBajaServicio, ConstructorXmlBajaServicio>();
builder.Services.AddScoped<IFirmadorXmlServicio, FirmadorXmlServicio>();
builder.Services.AddScoped<IProveedorCertificadoServicio, ProveedorCertificadoServicio>();
builder.Services.AddScoped<IEmpaquetadorZipServicio, EmpaquetadorZipServicio>();
builder.Services.AddScoped<IAlmacenamientoArchivosServicio, AlmacenamientoArchivosS3Servicio>();
builder.Services.AddHttpClient<ISunatBillServiceCliente, SunatBillServiceCliente>();
builder.Services.AddHttpClient<ISunatSummaryServiceCliente, SunatSummaryServiceCliente>();

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

// Casos de Uso — Cliente
builder.Services.AddScoped<InsertarClienteCasoDeUso>();
builder.Services.AddScoped<ObtenerClienteCasoDeUso>();
builder.Services.AddScoped<ListarClientesCasoDeUso>();
builder.Services.AddScoped<ActualizarClienteCasoDeUso>();
builder.Services.AddScoped<EliminarClienteCasoDeUso>();

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
builder.Services.AddScoped<EnviarDocumentoElectronicoASunatCasoDeUso>();
builder.Services.AddScoped<GuardarCambiosDocumentoElectronicoCasoDeUso>();
builder.Services.AddScoped<ActualizarEstadoCuotaDocumentoElectronicoCasoDeUso>();

// Casos de Uso — Certificado
builder.Services.AddScoped<InsertarCertificadoCasoDeUso>();
builder.Services.AddScoped<ObtenerCertificadoCasoDeUso>();
builder.Services.AddScoped<ListarCertificadosCasoDeUso>();
builder.Services.AddScoped<ActualizarCertificadoCasoDeUso>();
builder.Services.AddScoped<EliminarCertificadoCasoDeUso>();

// Casos de Uso — Credencial
builder.Services.AddScoped<InsertarCredencialCasoDeUso>();
builder.Services.AddScoped<ObtenerCredencialCasoDeUso>();
builder.Services.AddScoped<ListarCredencialesCasoDeUso>();
builder.Services.AddScoped<ActualizarCredencialCasoDeUso>();
builder.Services.AddScoped<EliminarCredencialCasoDeUso>();
builder.Services.AddScoped<DescifrarCredencialPorTipoCasoDeUso>();

// Casos de Uso — ConfiguracionFacturacionEmpresa
builder.Services.AddScoped<InsertarConfiguracionFacturacionEmpresaCasoDeUso>();
builder.Services.AddScoped<ObtenerConfiguracionFacturacionEmpresaCasoDeUso>();
builder.Services.AddScoped<ObtenerConfiguracionFacturacionEmpresaPorAmbienteCasoDeUso>();
builder.Services.AddScoped<ListarConfiguracionesFacturacionEmpresaCasoDeUso>();
builder.Services.AddScoped<ActualizarConfiguracionFacturacionEmpresaCasoDeUso>();
builder.Services.AddScoped<EliminarConfiguracionFacturacionEmpresaCasoDeUso>();

// Casos de Uso — LoteDocumento (Comunicación de Baja)
builder.Services.AddScoped<EnviarComunicacionBajaASunatCasoDeUso>();
builder.Services.AddScoped<ConsultarTicketComunicacionBajaCasoDeUso>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
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
