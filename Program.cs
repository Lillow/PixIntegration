namespace PixIntegration
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Instanciar a classe com as credenciais
                var bbHelper = new BancoDoBrasilHlp(
                    clientId: "eyJpZCI6ImEyOGU3ZDQyLTk5ZjAtNGRhYi1iYzIwLWYwN2NiY2VmMTJjNyIsImNvZGlnb1B1YmxpY2Fkb3IiOjAsImNvZGlnb1NvZnR3YXJlIjoxMjg4NTMsInNlcXVlbmNpYWxJbnN0YWxhY2FvIjoxfQ",
                    clientSecret: "eyJpZCI6IjY1MWQwMzUtZDQ3OC00IiwiY29kaWdvUHVibGljYWRvciI6MCwiY29kaWdvU29mdHdhcmUiOjEyODg1Mywic2VxdWVuY2lhbEluc3RhbGFjYW8iOjEsInNlcXVlbmNpYWxDcmVkZW5jaWFsIjoxLCJhbWJpZW50ZSI6ImhvbW9sb2dhY2FvIiwiaWF0IjoxNzQyMTUxMjM3NTI5fQ",
                    developerApplicationKey: "d30f742ae01ad6c1509b5a28ddd1be17");

                Cobranca cobranca;
                Resposta resposta;

                // Gerar um PIX imediato
                // O txid é opcional, se não for informado, será gerado automaticamente
                cobranca = new Cobranca(
                    chavePix: "hmtestes2@bb.com.br",
                    devedor: new Devedor(cnpj: "100.000.000-00", nome: "Devedor teste"),
                    valor: new Valor(100.00m),
                    calendario: new Calendario(expiracao: 3600)
                );
                resposta = bbHelper.GerarPIXimediato(cobranca).GetAwaiter().GetResult();
                Console.WriteLine("Resposta de GerarPix:");
                Console.WriteLine(resposta.Text + "\n\n");

                // cobranca = new Cobranca(chavePix: "hmtestes2@bb.com.br", devedor: new Devedor(cnpj: "100.000.000-00", nome: "Devedor teste"), valor: new Valor(100.00m), calendario: new Calendario(expiracao: 3600, validadeAposVencimento: 1));
                // resposta = bbHelper.GerarPIXvalVenc(cobranca).GetAwaiter().GetResult();
                // Console.WriteLine("Resposta de GerarPix:");
                // Console.WriteLine(resposta.Text + "\n\n");

                // Consultar cobranca
                resposta = bbHelper.ConsultarPIX("i2UiHdiBk386VRwkPmAC93oXSFPb0Uj6Nma").GetAwaiter().GetResult();
                Console.WriteLine("Resposta de ConsultarPix:");
                Console.WriteLine(resposta.Text + "\n\n");

                // Cancelar cobranca
                resposta = bbHelper.CancelarPIX("i2UiHdiBk386VRwkPmAC93oXSFPb0Uj6Nma").GetAwaiter().GetResult();
                Console.WriteLine("Resposta de CancelarPix:");
                Console.WriteLine(resposta.Text + "\n\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro: " + ex.Message);
            }
        }
    }
}