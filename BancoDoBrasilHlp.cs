using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace PixIntegration
{

    #region Class History
    /*--------------------------------------------------------------------
     * Created by : Danillo V. B. Silva
     * Version    : v1.0.0
     * Date       : 14/03/2025 ~ 00/00/2025
     * Purpose    : Integração com o banco do Brasil
     * -------------------------------------------------------------------
    */
    #endregion
    public class BancoDoBrasilHlp
    {
        #region Attributes
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string DeveloperApplicationKey { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.hm.bb.com.br/pix/v2/";
        #endregion

        #region Methods

        // Criar ou alterar um pix de cobrança imediata, se txid for informado ele altera o pix
        public async Task<Resposta> GerarPIXimediato(Cobranca requisicao)
        {
            Resposta resposta = new Resposta();

            HttpResponseMessage response = await CriarCobranca(requisicao);
            if (response == null)
                throw new Exception("Falha ao criar ou alterar a cobrança imediata: resposta nula.");

            if (response.IsSuccessStatusCode)
            {
                resposta.Cobranca = await Cobranca.Desserializar(response);
                resposta.Text = await response.Content.ReadAsStringAsync();
                return resposta;
            }
            resposta.Text = await response.Content.ReadAsStringAsync();
            throw new Exception(string.Format("Falha ao criar a cobrança: {0} - {1}", response.StatusCode, resposta.Text));
        }

        // Criar ou alterar um pix de cobrança com vencimento ou validade, se txid for informado ele altera o pix
        public async Task<Resposta> GerarPIXvalVenc(Cobranca requisicao)
        {
            Resposta resposta = new();

            HttpResponseMessage response = await CriarCobranca(requisicao);
            if (response == null)
                throw new Exception("Falha ao criar ou alterar a cobrança imediata: resposta nula.");

            if (response.IsSuccessStatusCode)
            {
                resposta.Cobranca = await Cobranca.Desserializar(response);
                resposta.Text = await response.Content.ReadAsStringAsync();
            }
            else
            {
                resposta.Text = await response.Content.ReadAsStringAsync();
                throw new Exception(string.Format("Falha ao criar a cobrança: {0} - {1}", response.StatusCode, resposta.Text));
            }

            if (requisicao.Calendario.DataDeVencimento != null || requisicao.Calendario.ValidadeAposVencimento != null)
            {
                if (string.IsNullOrEmpty(resposta.Cobranca.Txid))
                {
                    throw new Exception("Txid não pode ser nulo ou vazio.");
                }

                resposta.Cobranca.Calendario.DataDeVencimento = requisicao.Calendario.DataDeVencimento;
                resposta.Cobranca.Calendario.ValidadeAposVencimento = requisicao.Calendario.ValidadeAposVencimento;
                resposta.Cobranca.ChavePix = requisicao.ChavePix;

                // Cria a cobrança com vencimento
                response = await CriarCobranca(resposta.Cobranca);
                if (response == null)
                    throw new Exception("Falha ao criar a cobrança: resposta nula.");

                if (response.IsSuccessStatusCode)
                {
                    resposta.Cobranca = await Cobranca.Desserializar(response);
                    resposta.Text = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    resposta.Text = await response.Content.ReadAsStringAsync();
                    throw new Exception(string.Format("Falha ao criar a cobrança: {0} - {1}", response.StatusCode, resposta.Text));
                }
            }

            return resposta;
        }

        // Bucar um PIX de cobrança imediata por txid
        public async Task<Resposta> ConsultarPIX(String txid)
        {
            Resposta resposta = new();

            HttpResponseMessage response = await ConsultarCobranca(txid);
            if (response == null)
                throw new Exception("Falha ao criar a cobrança: resposta nula.");

            if (response.IsSuccessStatusCode)
            {
                resposta.Cobranca = await Cobranca.Desserializar(response);
                resposta.Text = await response.Content.ReadAsStringAsync();
            }
            else
            {
                resposta.Text = await response.Content.ReadAsStringAsync();
                throw new Exception(string.Format("Falha ao buscar a cobrança: {0} - {1}", response.StatusCode, resposta.Text));
            }

            return resposta;
        }

        // Cancelar um PIX de cobrança imediata por txid
        public async Task<Resposta> CancelarPIX(string txid)
        {
            HttpResponseMessage response = ConsultarCobranca(txid).Result ?? throw new Exception("Falha ao Buscar a cobrança: resposta nula.");
            Cobranca cobranca;

            if (response.IsSuccessStatusCode)
            {
                cobranca = await Cobranca.Desserializar(response);

                if (cobranca.Calendario.Expiracao != null)
                {
                    cobranca.Calendario.Expiracao = 1;

                    if (cobranca.Calendario.DataDeVencimento != null)
                    {
                        cobranca.Calendario.DataDeVencimento = null;
                    }
                    else if (cobranca.Calendario.ValidadeAposVencimento != null)
                    {
                        cobranca.Calendario.ValidadeAposVencimento = null;
                    }
                }
                else
                {
                    throw new Exception("Não é possível cancelar uma cobrança: Expiracao null.");
                }
            }
            else
            {
                throw new Exception(string.Format("Falha ao buscar a cobrança: {0} - {1}", response.StatusCode, response.Content.ReadAsStringAsync().Result));
            }

            response = await CriarCobranca(cobranca);
            if (response == null)
                throw new Exception("Falha ao alterar a cobrança: resposta nula.");

            Resposta resposta = new();
            if (response.IsSuccessStatusCode)
            {
                resposta.Cobranca = await Cobranca.Desserializar(response);
                resposta.Text = await response.Content.ReadAsStringAsync();
            }
            else
            {
                resposta.Text = await response.Content.ReadAsStringAsync();
                throw new Exception(string.Format("Falha ao cancelar a cobrança: {0} - {1}", response.StatusCode, resposta.Text));
            }

            return resposta;
        }

        // Metodos privados

        // Cria uma cobrança imediata, altera se o txid for informado ou tenta o put de cobv caso vencimento ou validade sejam informados
        private async Task<HttpResponseMessage> CriarCobranca(Cobranca requisicao)
        {
            if (requisicao == null || requisicao.Calendario == null)
                throw new ArgumentNullException("requisicao.Calendario");

            if (string.IsNullOrEmpty(AccessToken))
            {
                await ObterAccessToken();
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
                client.DefaultRequestHeaders.Add("gw-dev-app-key", DeveloperApplicationKey);

                String url = BaseUrl + "cob";
                DateTime? dataDeVencimento = requisicao.Calendario.DataDeVencimento;
                object validadeAposVencimento = requisicao.Calendario.ValidadeAposVencimento;
                if (requisicao.Txid == null)
                {
                    dataDeVencimento = null;
                    validadeAposVencimento = null;
                }
                else
                {
                    if (requisicao.Calendario.DataDeVencimento != null || requisicao.Calendario.ValidadeAposVencimento != null)
                    {
                        url = url + "v/" + requisicao.Txid;
                    }
                    else
                    {
                        url = url + "/" + requisicao.Txid;
                    }
                }

                var pixRequest = new
                {
                    calendario = new
                    {
                        expiracao = requisicao.Calendario.Expiracao,
                        dataDeVencimento = requisicao.Calendario.DataDeVencimento,
                        validadeAposVencimento = requisicao.Calendario.ValidadeAposVencimento,
                    },
                    devedor = requisicao.Devedor != null ? new
                    {
                        cnpj = requisicao.Devedor.Cnpj,
                        nome = requisicao.Devedor.Nome,
                        cpf = requisicao.Devedor.Cpf,
                        cep = requisicao.Devedor.Cep,
                        cidade = requisicao.Devedor.Cidade,
                        logradouro = requisicao.Devedor.Logradouro,
                        uf = requisicao.Devedor.Uf,
                    } : null,
                    valor = requisicao.Valor != null
                        ? new
                        {
                            original = requisicao.Valor.Original.ToString("N2", new CultureInfo("en-US")),
                        }
                        : null,
                    chave = requisicao.ChavePix,
                    solicitacaoPagador = requisicao.SolicitacaoPagador,
                    infoAdicionais = requisicao.InfoAdicionais != null
                        ? requisicao.InfoAdicionais.Select(info => new { nome = info.Nome, valor = info.Valor }).ToArray()
                        : null
                };

                String json = JsonConvert.SerializeObject(pixRequest);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    if (requisicao.Txid != null)
                    {
                        return await client.PutAsync(url, content);
                    }
                    return await client.PostAsync(url, content);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine("Falha na comunicação: " + ex.Message);
                    throw;
                }
            }
        }

        // Consulta uma cobrança, se não encontrar tenta a consulta de vencimento
        private async Task<HttpResponseMessage> ConsultarCobranca(string txid)
        {
            // Verifica se o AccessToken está válido
            if (string.IsNullOrEmpty(AccessToken))
            {
                await ObterAccessToken();
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
                client.DefaultRequestHeaders.Add("gw-dev-app-key", DeveloperApplicationKey);

                var url = BaseUrl + "cob/" + txid;

                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        response = await client.GetAsync(BaseUrl + "cobv/" + txid);
                    }

                    return response;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine("Falha na comunicação: " + ex.Message);
                    throw;
                }
            }
        }

        // Método para obter o AccessToken
        private async Task ObterAccessToken()
        {
            using (var client = new HttpClient())
            {
                var authUrl = "https://oauth.hm.bb.com.br/oauth/token";
                var authData = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "scope", "cob.write cob.read pix.write pix.read" }
                };

                // Codificar ClientId e ClientSecret em Base64
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(ClientId + ":" + ClientSecret));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                // Adicionar o cabeçalho X-Developer-Application-Key
                client.DefaultRequestHeaders.Add("X-Developer-Application-Key", DeveloperApplicationKey);

                // Enviar a requisição POST para obter o AccessToken
                var response = await client.PostAsync(authUrl, new FormUrlEncodedContent(authData));
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonConvert.DeserializeObject<dynamic>(content);
                    if (tokenResponse != null)
                    {
                        this.AccessToken = tokenResponse.access_token;
                    }
                    else
                    {
                        throw new Exception("Falha ao obter o AccessToken: resposta nula.");
                    }
                    Console.WriteLine("AccessToken obtido com sucesso!\n\n");
                }
                else
                {
                    throw new Exception("Falha ao obter o AccessToken: " + content);
                }
            }
        }

        private bool ValidarChavePix(string chave)
        {
            // Validação para email
            if (chave.Contains("@"))
            {
                return Regex.IsMatch(chave, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
            }

            // Validação para CPF/CNPJ
            // ... outras validações

            return true;
        }

        #endregion

        #region Constructors
        public BancoDoBrasilHlp(string clientId, string clientSecret, string developerApplicationKey)
        {
            if (clientId == null) throw new ArgumentNullException("clientId");
            if (clientSecret == null) throw new ArgumentNullException("clientSecret");
            if (developerApplicationKey == null) throw new ArgumentNullException("developerApplicationKey");

            ClientId = clientId;
            ClientSecret = clientSecret;
            DeveloperApplicationKey = developerApplicationKey;
            AccessToken = string.Empty;
            BaseUrl = "https://api.hm.bb.com.br/pix/v2/";

            // Obter token de forma síncrona para construtor
            ObterAccessToken().Wait();
        }
        #endregion
    }

    // Classe para a resposta da cobrança
    public class Resposta
    {
        #region Attributes
        public string Text { get; set; }
        public Cobranca Cobranca { get; set; }
        #endregion

        #region Constructors
        public Resposta()
        {
            Text = string.Empty;
            Cobranca = null;
        }
        #endregion
    }

    // Classe para a requisição de cobrança
    public class Cobranca
    {
        #region Attributes
        public Calendario Calendario { get; set; }
        public string Txid { get; set; }
        public string Revisao { get; set; }
        public Loc Loc { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public Devedor Devedor { get; set; }
        public Valor Valor { get; set; }
        public string ChavePix { get; set; }
        public string SolicitacaoPagador { get; set; }
        public InfoAdicional[] InfoAdicionais { get; set; }
        #endregion

        #region Constructors
        public Cobranca()
        {
            Calendario = new Calendario();
            Txid = null;
            Revisao = null;
            Loc = null;
            Location = null;
            Status = null;
            Devedor = null;
            Valor = null;
            ChavePix = null;
            SolicitacaoPagador = null;
            InfoAdicionais = null;
        }

        public Cobranca(string chavePix, Devedor devedor, Valor valor, Calendario calendario,
                       string solicitacaoPagador = "Serviço realizado.",
                       InfoAdicional[] infoAdicionais = null)
        {
            ChavePix = chavePix ?? throw new ArgumentNullException("chavePix");
            Devedor = devedor ?? throw new ArgumentNullException("devedor");
            Valor = valor ?? throw new ArgumentNullException("valor");
            Calendario = calendario ?? throw new ArgumentNullException("calendario");
            SolicitacaoPagador = solicitacaoPagador;
            InfoAdicionais = infoAdicionais;
        }
        #endregion

        #region Methods
        public static Task<Cobranca> Desserializar(HttpResponseMessage response)
        {
            if (response == null)
            {
                throw new ArgumentNullException("response", "A resposta não pode ser nula.");
            }
            else if (!response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().Result;
                throw new InvalidOperationException(string.Format("Erro na resposta: {0} - {1}", response.StatusCode, content));
            }
            var json = response.Content.ReadAsStringAsync().Result;
            var obj = JsonConvert.DeserializeObject<Cobranca>(json);
            if (obj == null)
                throw new InvalidOperationException("A desserialização resultou em um objeto nulo.");
            return Task.FromResult(obj);
        }
        #endregion

    }

    // Classe para o calendário
    public class Calendario
    {
        #region Attributes
        public long? Expiracao { get; set; }
        public DateTime? DataDeVencimento { get; set; }
        public long? ValidadeAposVencimento { get; set; }
        #endregion

        #region Constructors

        public Calendario()
        {
            Expiracao = null;
            DataDeVencimento = null;
            ValidadeAposVencimento = null;
        }
        public Calendario(long expiracao)
        {
            Expiracao = expiracao;
            DataDeVencimento = null;
            ValidadeAposVencimento = null;
        }
        public Calendario(long expiracao, DateTime dataDeVencimento)
        {
            Expiracao = expiracao;
            DataDeVencimento = dataDeVencimento;
            ValidadeAposVencimento = null;
        }
        public Calendario(long expiracao, long validadeAposVencimento)
        {
            Expiracao = expiracao;
            DataDeVencimento = null;
            ValidadeAposVencimento = validadeAposVencimento;
        }

        #endregion
    }

    // Classe para o locatário
    public class Loc
    {
        #region Attributes
        public long? Id { get; set; }
        public string Location { get; set; }
        public string TipoCob { get; set; }
        #endregion

        #region Constructors

        public Loc()
        {
            Id = null;
            Location = null;
            TipoCob = null;
        }

        public Loc(long? id, string location, string tipoCob)
        {
            Id = id;
            Location = location;
            TipoCob = tipoCob;
        }
        #endregion
    }

    // Classes para o valor
    public class Valor
    {
        #region Attributes
        public decimal Original { get; set; }
        public Multa Multa { get; set; }
        public Juros Juros { get; set; }
        public Desconto Desconto { get; set; }
        public DescontoDataFixa[] DescontoDataFixa { get; set; }
        #endregion

        #region Constructors
        public Valor()
        {
            // Inicialize como null para tipos referência, se desejar
            Multa = null;
            Juros = null;
            Desconto = null;
            DescontoDataFixa = null;
        }
        public Valor(decimal original, Multa multa = null, Juros juros = null, Desconto desconto = null, DescontoDataFixa[] descontoDataFixa = null)
        {
            Original = original;
            Multa = multa;
            Juros = juros;
            Desconto = desconto;
            DescontoDataFixa = descontoDataFixa;
        }
        #endregion
    }
    public class Multa
    {
        #region Attributes
        public int? Modalidade { get; set; }
        public decimal? ValorPerc { get; set; }
        #endregion

        #region Constructors
        public Multa()
        {
            Modalidade = null;
            ValorPerc = null;
        }
        public Multa(int modalidade, decimal? valorPerc = null)
        {
            Modalidade = modalidade;
            ValorPerc = valorPerc;
        }
        #endregion
    }
    public class Juros
    {
        #region Attributes
        public int? Modalidade { get; set; }
        public decimal? ValorPerc { get; set; }
        #endregion

        #region Constructors

        public Juros()
        {
            Modalidade = null;
            ValorPerc = null;
        }
        public Juros(int modalidade, decimal? valorPerc = null)
        {
            Modalidade = modalidade;
            ValorPerc = valorPerc;
        }

        #endregion
    }
    public class Desconto
    {
        #region Attributes
        public int? Modalidade { get; set; }
        public DescontoDataFixa[] DescontoDataFixa { get; set; }
        #endregion

        #region Constructors
        public Desconto()
        {
            Modalidade = null;
            DescontoDataFixa = null;
        }

        public Desconto(int modalidade, DescontoDataFixa[] descontoDataFixa = null)
        {
            Modalidade = modalidade;
            DescontoDataFixa = descontoDataFixa;
        }
        #endregion
    }
    public class DescontoDataFixa
    {
        #region Attributes
        public DateTime? Data { get; set; }
        public decimal? ValorPerc { get; set; }
        #endregion

        #region Constructors

        public DescontoDataFixa()
        {
            Data = null;
            ValorPerc = null;
        }

        public DescontoDataFixa(DateTime? data, decimal? valorPerc = null)
        {
            Data = data;
            ValorPerc = valorPerc;
        }
        #endregion
    }

    // Classe para o devedor
    public class Devedor
    {
        #region Attributes
        public string Cnpj { get; set; }
        public string Cpf { get; set; }
        public string Nome { get; set; }
        public string Logradouro { get; set; }
        public string Cidade { get; set; }
        public string Uf { get; set; }
        public string Cep { get; set; }
        #endregion

        #region Constructors

        public Devedor()
        {
            Cnpj = null;
            Cpf = null;
            Nome = null;
            Logradouro = null;
            Cidade = null;
            Uf = null;
            Cep = null;
        }

        public Devedor(string cnpj = null, string nome = null, string cpf = null, string logradouro = null, string cidade = null, string uf = null, string cep = null)
        {
            Cnpj = cnpj;
            Cpf = cpf;
            Nome = nome;
            Logradouro = logradouro;
            Cidade = cidade;
            Uf = uf;
            Cep = cep;
        }

        #endregion
    }

    public class InfoAdicional
    {
        #region Attributes
        public string Nome { get; set; }
        public string Valor { get; set; }
        #endregion

        #region Constructors
        public InfoAdicional()
        {
            Nome = null;
            Valor = null;
        }

        public InfoAdicional(string nome, string valor)
        {
            Nome = nome;
            Valor = valor;
        }
        #endregion
    }
}