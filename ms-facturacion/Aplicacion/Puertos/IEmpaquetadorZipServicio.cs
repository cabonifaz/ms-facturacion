namespace ms_facturacion.Aplicacion.Puertos;

public interface IEmpaquetadorZipServicio
{
    byte[] Empaquetar(string nombreArchivoXml, byte[] xmlFirmado);
}
