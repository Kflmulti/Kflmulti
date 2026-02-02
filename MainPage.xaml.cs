using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using Plugin.LocalNotification;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Kflmulti
{
    public partial class MainPage : ContentPage
    {
        public BulkObservableCollection<ClientesHoje> ClientesExibidos { get; set; } = new();

        private double _saldoContaInformado = 0;
        private double _gastoCartaoInformado = 0;
        private string _dataUltimoFinanceiro = "";
        private bool _meuAnuncioAtivo = true;
        private double _impostoBaseAtivaTotal = 0;

        private List<ClientesHoje> _listaAtivosOk = new();
        private List<ClientesHoje> _listaPendentesLocal = new();
        private List<ClientesHoje> _listaVenceHoje = new();
        private List<ClientesHoje> _listaCompletaServidor = new();

        private static readonly HttpClient _stockHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        private const string KEY_FUNDO_SALDO = "fundo_investimento_saldo";
        private const string KEY_FUNDO_ULTIMO_DIA = "fundo_investimento_ultimo_dia";
        private const string KEY_INVESTMENTS = "investments_list";
        private const string KEY_PAUSADOS_HOJE = "pausados_hoje";
        private const string KEY_RENOVADOS_HOJE = "renovados_hoje";
        private const string KEY_NOVOS_HOJE = "novos_hoje";
        private const string KEY_RETORNADOS_HOJE = "retornados_hoje";
        private const string KEY_PENDENTES_PAGOS = "pendentes_pagos";
        // Adicione junto às outras chaves/preferences (campos no topo da classe)
        private const string KEY_TOTAL_NF_MES = "total_nf_mes_acumulado";
        private double _totalNfMesPersistido = 0.0;

        private const string KEY_FIXED = "fin_fixed_expenses";
        private const string KEY_VAR = "fin_var_expenses";
        private const string KEY_VARRED = "fin_var_expenses_red";
        private const string KEY_CUSTO_POR_DIA = "custo_por_dia_anuncio";            // novo: custo por dia de cada anúncio
        private const string KEY_CUSTO_ANUNCIOS_MES = "custo_anuncios_total_mes";   // novo: acumulado mensal de custo de anúncios

        // Adicione junto às outras constantes/fields no topo da classe MainPage
        private const double TAXA_DIARIA_CDB = 0.000261; // 0,0261% ao dia

        private double jurosCdbPessoal = 0.0;
        private double jurosCdbEmpresa = 0.0;
        // Adicione estas declarações entre os campos privados da classe MainPage (perto de _fundoSaldo, _investments etc.)
        private Label? _labelGanhoCdbPessoal;
        private Label? _labelGanhoCdbEmpresa;

        private const string KEY_JUROS_CDB_PESSOAL = "juros_cdb_pessoal";
        private const string KEY_JUROS_CDB_EMPRESA = "juros_cdb_empresa";
        private const string KEY_JUROS_CDB_ULTIMO_DIA = "juros_cdb_ultimo_dia";


        private const string KEY_PENDING_COMMANDS = "pending_http_commands";
        private List<PendingHttpCommand> _pendingHttpCommands = new();

        private Border? _badgeBorderAtualizar;
        private Label? _badgeLabelAtualizar;


        private double _fundoSaldo = 0.0;
        private double _SaldoFake = 320000;
        bool fake = true;
        private List<InvestmentCard> _investments = new();


        private Grid _containerConteudo = null!;
        private ListView _listView = null!;
        private Entry _searchEntry = null!;
        private Grid _layoutPrincipal = null!;
        private ActivityIndicator _loader = null!;

        // campos: cor inicial do label de faturamento e do meu anúncio (ajustados)
        Label labelFaturamento = new Label { Text = "Faturamento: R$ 0,00", TextColor = Color.FromArgb("#1B5E20") };
        Label labelImposto = new Label { Text = "Imposto (6%): R$ 0,00", TextColor = Colors.Red };
        Label labelCustoAnuncio = new Label { Text = "Custo em anuncios: R$ 0,00", TextColor = Colors.Red };
        Label labelSaldoLiquido = new Label { Text = "Líquido: R$ 0,00", TextColor = Colors.Green, FontAttributes = FontAttributes.Bold };
        Label labelMeuAnuncio = new Label { Text = "Meu Anúncio: R$ 0,00", TextColor = Colors.Gray, FontAttributes = FontAttributes.Bold };

        private List<ClientesHoje> _listaRenovadosHoje = new();
        private List<NfModel> _listaNfLocal = new();
        private List<string> _listaPendentesPagos = new();

        private List<FixedExpense> _fixedExpenses = new();
        private List<VariableExpense> _variableExpenses = new();
        private List<VariableExpense> _variableExpensesReds = new();

        private int _metaFixaDoDia = 0;

        private System.Timers.Timer _searchTimer;
        private string _modoAtual = "ATIVOS";
        private List<string> _listaPausadosHoje = new();
        private List<string> _listaNovosHoje = new();
        private List<string> _listaRetornadosHoje = new();
        private List<string> _planosDisponiveis = new()
        {
            "Plano GOLD - R$ 150,00", "Combo GOLD SMART - R$ 300,00", "Combo GOLD ADVANCED - R$ 600,00",
            "Plano MEGA - R$ 300,00", "Combo MEGA SMART - R$ 600,00", "Combo MEGA ADVANCED - R$ 1.200,00",
            "Plano PLUS - R$ 600,00", "Combo PLUS SMART - R$ 1.200,00", "Combo PLUS ADVANCED - R$ 2.400,00"
        };

        

        private Frame _floatBtn = null!;
        private Frame _floatBtnG = null!;

        public MainPage()
        {
            InicializarInterface();

            // carrega despesas persistidas (fixas e variáveis)
            CarregarDespesasFinancas();

            var jsonNf = Preferences.Default.Get("lista_nf_salva", "");
            if (!string.IsNullOrEmpty(jsonNf)) _listaNfLocal = JsonConvert.DeserializeObject<List<NfModel>>(jsonNf);

            _totalNfMesPersistido = Preferences.Default.Get(KEY_TOTAL_NF_MES, 0.0);

            _searchTimer = new System.Timers.Timer(400);
            _searchTimer.AutoReset = false;
            _searchTimer.Elapsed += (s, e) => MainThread.BeginInvokeOnMainThread(ExecutarBuscaReal);

            // carrega estado do meu anúncio persistido
            _meuAnuncioAtivo = Preferences.Default.Get("meu_anuncio_ativo", _meuAnuncioAtivo);
           
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1) carregar fila persistida primeiro (importante para sobreviver a reinícios)
            CarregarPendingCommandsFromPrefs();
            AtualizarBadgePendentes();

            try
            {
                // garante persistência final da fila antes da página fechar
                await SalvarPendingCommandsToPrefsAsync();
            }
            catch
            {
                // ignorar erros de persistência para não quebrar o fluxo de UI
            }

            await Task.Delay(100);
            await CarregarDadosServidor();
            CarregarPausadosDoDispositivo();
            CarregarRenovadosDoDispositivo();
            CarregarNovosDoDispositivo();
            CarregarRetornadosDoDispositivo();
            CarregarPendentesPagosDoDispositivo();
            CarregarPendentesDoDispositivo();

            try
            {
                ProcessarListas();
                AtualizarDashboardFinanceiro();
                // se estiver na lista principal, atualizar exibição
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _modoAtual = "ATIVOS";
                    _searchEntry.Text = string.Empty;
                    ExecutarBuscaReal();
                });
            }
            catch
            {
                // ignorar erros de UI/atualização para não travar o OnAppearing
            }

            VerificarFechamentoMensal();

            CarregarInvestimentos();
            // registra custo diário do meu anúncio se ainda não registrado hoje (apenas na virada do dia)
            // Carrega juros persistidos e registra juros diários dos CDBs se ainda não registrado hoje.
            // Isso garante acumulação diária mesmo sem abrir a tela "Investimentos".
            try
            {
                CarregarJurosCdbFromPrefs();
                await RegistrarJurosCdbIfNeeded();
            }
            catch
            {
                // não falhar o OnAppearing se algo der errado com juros
            }

            RegistrarCustoDiarioMeuAnuncioIfNeeded();
            RegistrarCustoDiarioFundoIfNeeded();   
                           

            // garante que "Entrada Hoje" zere apenas na virada do dia
            VerificarResetDiarioEntradaHoje();

            AtualizarDashboardFinanceiro();
#if ANDROID
            _ = InitializeNotificationsAsync();
#endif


           //await ClearAppCacheAsync();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            try
            {
                await SalvarPendingCommandsToPrefsAsync();
            }
            catch
            {
                // ignorar erros para não quebrar fluxo de UI
            }
        }
        private void InicializarInterface()
        {
            var bg = Color.FromArgb("#F6F8FB");
            _containerConteudo = new Grid { BackgroundColor = bg };

            _loader = new ActivityIndicator
            {
                IsRunning = false,
                IsVisible = false,
                Color = Color.FromArgb("#1976D2"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                WidthRequest = 50,
                HeightRequest = 50,
                ZIndex = 2
            };

            _layoutPrincipal = new Grid
            {
                RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star } },
                Padding = new Thickness(12),
                BackgroundColor = bg
            };

            _listView = new ListView(ListViewCachingStrategy.RecycleElement)
            {
                ItemsSource = ClientesExibidos,
                HasUnevenRows = true,
                SeparatorVisibility = SeparatorVisibility.None,
                ItemTemplate = CriarItemTemplate(),
                BackgroundColor = Colors.Transparent,
                SelectionMode = ListViewSelectionMode.None
            };
            _listView.ItemTapped += async (s, e) => { if (e.Item is ClientesHoje cliente) await AoTocarNoCliente(cliente); };

            _layoutPrincipal.Add(CriarCabecalho(), 0, 0);
            _layoutPrincipal.Add(_listView, 0, 1);

            _containerConteudo.Children.Add(_layoutPrincipal);

            var gridMaster = new Grid();
            gridMaster.Children.Add(_containerConteudo);
            gridMaster.Children.Add(_loader);



            // --- Botão flutuante no inicializador (campo) ---
            _floatBtn = new Frame
            {
                WidthRequest = 56,
                HeightRequest = 56,
                CornerRadius = 28,
                BackgroundColor = Color.FromArgb("#1F1F1F"), // preto metálico
                HasShadow = true,
                Padding = 0,
                Content = new Label { Text = "+", TextColor = Colors.White, FontSize = 28, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 14, 18),
                IsVisible = true // visível na tela inicial
            };
            var tapFloat = new TapGestureRecognizer();
            tapFloat.Tapped += async (s, e) => { await AddVariableExpensePrompt(); };
            _floatBtn.GestureRecognizers.Add(tapFloat);
            gridMaster.Children.Add(_floatBtn);

            _floatBtnG = new Frame
            {
                WidthRequest = 56,
                HeightRequest = 56,
                CornerRadius = 28,
                BackgroundColor = Color.FromArgb("#006400"), // Verde escuro (DarkGreen)
                HasShadow = true,
                Padding = 0,
                Content = new Label { Text = "G", TextColor = Colors.White, FontSize = 28, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 14, 78),
                IsVisible = true // visível na tela inicial
            };

            if (_floatBtn.IsVisible) _floatBtnG.Margin = new Thickness(0, 0, 14, 78);
            else _floatBtnG.Margin = new Thickness(0, 0, 14, 18);

            var tapFloatG = new TapGestureRecognizer();
            tapFloatG.Tapped += async (s, e) => { await AbrirGestao(); };
            _floatBtnG.GestureRecognizers.Add(tapFloatG);
            gridMaster.Children.Add(_floatBtnG);

            Content = gridMaster;
        }
        private View CriarCabecalho()
        {
            _searchEntry = new Entry
            {
                Placeholder = "🔎 Buscar histórico...",
                FontSize = ObterFonteResponsiva("🔎 Buscar histórico...", 14, 10),
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Black,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0)
            };
            _searchEntry.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };

            var icon = new Label
            {
                Text = "🔎",
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                FontSize = ObterFonteResponsiva("🔎", 18, 12),
                Margin = new Thickness(6, 0, 6, 0)
            };

            var searchGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 4,
                VerticalOptions = LayoutOptions.Center
            };
            searchGrid.Add(icon, 0, 0);
            searchGrid.Add(_searchEntry, 1, 0);

            var searchFrame = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(20) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#E6EEF8"),
                Padding = new Thickness(10, 6),
                Content = searchGrid
            };

            var gridBotoes = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 8 };
            gridBotoes.Add(CriarBotaoAtualizarComBadge(async () => await CarregarDadosServidor()), 0);
            gridBotoes.Add(CriarBotaoEstilizado("PENDENTES", Color.FromArgb("#E53935"), OnBotaoPendentesClicked), 1);
            gridBotoes.Add(CriarBotaoEstilizado("HOJE", Color.FromArgb("#2E7D32"), OnBotaoHojeClicked), 2);
            gridBotoes.Add(CriarBotaoEstilizado("OPER", Color.FromArgb("#FF9800"), AbrirTelaOperacional), 3);

            var header = new VerticalStackLayout { Spacing = 10 };
            header.Children.Add(searchFrame);
            header.Children.Add(gridBotoes);
            return header;
        }
        private DataTemplate CriarItemTemplate()
        {
            return new DataTemplate(() =>
            {
                var avatarLabel = new Label
                {
                    Text = "?",
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var avatar = new Frame
                {
                    WidthRequest = 48,
                    HeightRequest = 48,
                    CornerRadius = 24,
                    BackgroundColor = Color.FromArgb("#1976D2"),
                    HasShadow = false,
                    Padding = 0,
                    Content = avatarLabel
                };

                var nome = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 16, // antes 15 -> +1
                    TextColor = Colors.Black,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1,
                    HorizontalTextAlignment = TextAlignment.Start
                };
                nome.SetBinding(Label.TextProperty, "Cliente");

                var info = new Label
                {
                    FontSize = 13, // antes 12 -> +1
                    TextColor = Colors.DimGray,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1,
                    HorizontalTextAlignment = TextAlignment.Start
                };
                info.SetBinding(Label.TextProperty, "InfoLista");

                var rightStack = new VerticalStackLayout { Spacing = 2 };
                rightStack.Children.Add(nome);
                rightStack.Children.Add(info);

                var contentGrid = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 12
                };
                contentGrid.Add(avatar, 0, 0);
                contentGrid.Add(rightStack, 1, 0);

                var vistoBadge = new Border
                {
                    StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                    Background = Color.FromArgb("#2E7D32"),
                    Padding = new Thickness(6, 2),
                    Content = new Label { Text = "✔ Visto", TextColor = Colors.White, FontSize = 11 },
                    IsVisible = false,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, -6, -6, 0)
                };
                // bind direto à propriedade — assim atualiza quando ContatoFeitoHoje muda
                vistoBadge.SetBinding(Border.IsVisibleProperty, nameof(ClientesHoje.ContatoFeitoHoje));

                var container = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star } },
                    RowDefinitions = { new RowDefinition { Height = GridLength.Auto } }
                };
                container.Add(contentGrid, 0, 0);
                container.Add(vistoBadge, 0, 0);

                var card = new Border
                {
                    StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(12) },
                    Background = Colors.White,
                    Stroke = Color.FromArgb("#E6EEF8"),
                    Padding = 12,
                    Margin = new Thickness(6, 6),
                    Content = container
                };

                card.BindingContextChanged += (s, e) =>
                {
                    if (s is Border f && f.BindingContext is ClientesHoje cl)
                    {
                        var lbl = avatar.Content as Label;
                        if (lbl != null)
                        {
                            if (!string.IsNullOrWhiteSpace(cl.Cliente))
                            {
                                var parts = cl.Cliente.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                var initials = parts.Length == 1 ? parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant()
                                                                 : (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpperInvariant();
                                lbl.Text = initials;
                            }
                            else lbl.Text = "?";
                        }

                        nome.FontSize = ObterFonteResponsiva(cl.Cliente ?? "", 15, 11);
                        info.FontSize = ObterFonteResponsiva(cl.InfoLista ?? "", 12, 10);

                        // calcula 'vencido' com prioridade para Datapg (quando pendente), senão usa Fim
                        bool vencido = false;
                        DateTime hoje = DateTime.Now.Date;

                        DateTime? tentativa = null;

                        // tenta Datapg primeiro (geralmente usado para pendentes)
                        try
                        {
                            var dp = (cl.Datapg ?? "").Replace("\\", "").Trim();
                            if (!string.IsNullOrEmpty(dp))
                            {
                                string candidate = dp;
                                if (candidate.Count(ch => ch == '/') == 1) candidate += "/" + DateTime.Now.Year.ToString();
                                if (DateTime.TryParseExact(candidate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) tentativa = dt;
                                else if (DateTime.TryParse(candidate, out var dt2)) tentativa = dt2;
                            }
                        }
                        catch { tentativa = null; }

                        // se não obteve Datapg válido, tenta Fim
                        if (tentativa == null)
                        {
                            try
                            {
                                var fimRaw = (cl.Fim ?? "").Replace("\\", "").Trim();
                                if (!string.IsNullOrEmpty(fimRaw))
                                {
                                    string fimParaParse = fimRaw;
                                    if (fimParaParse.Count(ch => ch == '/') == 1) fimParaParse += "/" + DateTime.Now.Year.ToString();

                                    if (DateTime.TryParseExact(fimParaParse, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dtFim))
                                        tentativa = dtFim;
                                    else if (DateTime.TryParse(fimParaParse, out DateTime dtFim2))
                                        tentativa = dtFim2;
                                }
                            }
                            catch { tentativa = null; }
                        }

                        if (tentativa != null)
                        {
                            vencido = tentativa.Value.Date <= hoje;
                        }

                        // estilo: se pendente e vencido -> destaque vermelho forte
                        if (cl.IsPendente)
                        {
                            avatar.BackgroundColor = Colors.Transparent;
                            if (lbl != null) lbl.TextColor = Color.FromArgb("#1976D2");

                            if (vencido)
                            {
                                f.Stroke = Color.FromArgb("#D32F2F");
                                f.Background = Color.FromArgb("#FFEBEE");
                            }
                            else
                            {
                                f.Stroke = Color.FromArgb("#FFCDD2");
                                f.Background = Color.FromArgb("#FFF8F8");
                            }
                        }
                        else
                        {
                            avatar.BackgroundColor = Color.FromArgb("#1976D2");
                            if (lbl != null) lbl.TextColor = Colors.White;
                            f.Stroke = Color.FromArgb("#E6EEF8");
                            f.Background = Colors.White;
                        }
                    }
                };

                return new ViewCell { View = card };
            });
        }


        #region Class
        public class ClientesHoje : INotifyPropertyChanged
        {
            private bool _contatoFeitoHoje;
            public bool ContatoFeitoHoje { get => _contatoFeitoHoje; set { _contatoFeitoHoje = value; OnPropertyChanged(); } }
            public string Cliente { get; set; } = "";
            public string Plano { get; set; } = "";
            public string Inicio { get; set; } = "";
            public string Fim { get; set; } = "";
            public string Situacao { get; set; } = "";
            public string Cel { get; set; } = "";
            public string Celcon { get; set; } = "";
            public string Ativo { get; set; } = "";
            public string Datapg { get; set; } = "";
            public string Pg { get; set; } = "";
            public bool IsPendente { get; set; }
            public DateTime DataPagamentoPendente { get; set; }
            public string InfoLista => IsPendente ? $"📅 {DataPagamentoPendente:dd/MM} - {Pg} | {Plano}" : Plano;
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public class RelatorioFinanceiro { public double FaturamentoTotal { get; set; } public double CustoAnuncio { get; set; } public double ImpostosTotal { get; set; } public int TotalRenovacoes { get; set; } public double MeuAnuncioTotal { get; set; } }
        public class NfModel { public string Data { get; set; } = ""; public string Cliente { get; set; } = ""; public string Valor { get; set; } = ""; public string Plano { get; set; } = ""; public int Dias { get; set; } }
        public class FixedExpense
        {
            public int DayOfMonth { get; set; }       // dia do mês em que o gasto passa a vigorar
            public string Name { get; set; } = "";
            public double Value { get; set; }
            public bool Included { get; set; } = false; // visto / já está sendo descontado
        }
        public class VariableExpense
        {
            public string Date { get; set; } = DateTime.Now.ToString("dd/MM/yyyy"); // sempre dia atual
            public string Description { get; set; } = "";
            public double Value { get; set; }
        }
        public class InvestmentCard
        {
            public string Name { get; set; } = "";
            public double Quantity { get; set; } = 0; // total de cotas
            public double PricePerUnit { get; set; } = 0; // último preço por cota
            public List<PurchaseHistory> History { get; set; } = new();
        }
        public class PurchaseHistory
        {
            public string Date { get; set; } = DateTime.Now.ToString("dd/MM/yyyy");
            public double Quantity { get; set; }
            public double TotalInvested { get; set; }
        }
        public class BulkObservableCollection<T> : ObservableCollection<T>
        {
            public void ReplaceRange(IEnumerable<T> collection) { Items.Clear(); foreach (var item in collection) Items.Add(item); OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)); }
        }

        #endregion

        // -------------------------TELAS------------------------------//


        #region Tela Gestão
        private async Task AbrirGestao()
        {
            var background = Color.FromArgb("#FFFFFF");
            var layout = new VerticalStackLayout
            {
                Padding = 14,
                Spacing = 10,
                BackgroundColor = background
            };

            // Título
            layout.Add(new Label
            {
                Text = "GESTÃO",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Colors.Black
            });

            // Botões principais
            layout.Add(CriarBotaoEstilizado("OPERACIONAL", Color.FromArgb("#1976D2"),
                async () => AbrirTelaOperacional()));

            layout.Add(CriarBotaoEstilizado("GESTÃO FINANCEIRA", Color.FromArgb("#2E7D32"),
                async () => await AbrirGestaoFinanceira()));

            layout.Add(CriarBotaoEstilizado("FINANÇA PESSOAL", Color.FromArgb("#0D47A1"),
                async () => await AbrirFinancaPessoal(0))); // pode passar 0 ou outro valor de lucroProjetadoMes

            layout.Add(CriarBotaoEstilizado("INVESTIMENTOS", Color.FromArgb("#455A64"),
                async () => AbrirInvestimentos()));

            // Botão de limpeza de cache (preservando investimentos)
            layout.Add(CriarBotaoEstilizado("🧹 LIMPAR CACHE", Color.FromArgb("#E53935"),
                async () => await ClearAppCacheAsync()));

            // Botão de voltar
            layout.Add(CriarBotaoEstilizado("TELA PRINCIPAL", Color.FromArgb("#9E9E9E"),
                async () => VoltarParaLista()));

            // ScrollView para rolagem
            var scroll = new ScrollView { BackgroundColor = background, Content = layout };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(scroll);
                _floatBtn.IsVisible = false;
                _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
            });
        }

        #endregion

        #region Tela Operacional
        //#endregion
        private void AbrirTelaOperacional()
        {
            var background = Color.FromArgb("#F7FAFE");
            var scroll = new ScrollView { BackgroundColor = background };
            var layout = new VerticalStackLayout { Padding = 18, Spacing = 14, BackgroundColor = background };

            VerificarResetDiarioEntradaHoje();

            int totalHoje = _listaCompletaServidor.Count;
            int totalOntem = Preferences.Default.Get("total_clientes_ontem", totalHoje);
            int renovadosCount = _listaRenovadosHoje?.Count ?? 0;
            int pausadosCount = _listaPausadosHoje?.Count ?? 0;
            int pendentesCount = _listaPendentesLocal?.Count ?? 0;

            double custoMeuAnuncioHoje = _meuAnuncioAtivo ? 100.00 : 0.00;

            // calcula quantos clientes são "novos" comparando com o cache anterior
            // calcula quantos clientes são "novos" comparando com o cache anterior
            int novosClientes = 0;
            List<string> novosLista = new();
            try
            {
                var prevJson = Preferences.Default.Get("lista_clientes_cache_prev", "");
                if (!string.IsNullOrEmpty(prevJson))
                {
                    var prevList = JsonConvert.DeserializeObject<List<ClientesHoje>>(prevJson) ?? new List<ClientesHoje>();
                    var prevNames = new HashSet<string>(prevList.Where(x => !string.IsNullOrEmpty(x.Cliente)).Select(x => x.Cliente.Trim()), StringComparer.OrdinalIgnoreCase);
                    var encontrados = _listaCompletaServidor
                        .Where(c => !string.IsNullOrEmpty(c.Cliente) && !prevNames.Contains(c.Cliente.Trim()))
                        .Select(c => c.Cliente.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    novosLista = encontrados;
                    novosClientes = encontrados.Count;

                    // persiste os nomes para ficar estável ao longo do dia
                    // persiste individualmente para garantir atualização imediata no relatório
                    foreach (var n in encontrados)
                    {
                        AddNovo(n);
                    }
                    // após o foreach (var n in encontrados) { AddNovo(n); }
                    novosClientes = _listaNovosHoje?.Count ?? 0;
                }
                else
                {
                    novosClientes = totalHoje > totalOntem ? totalHoje - totalOntem : 0;
                    // sem cache anterior não é possível identificar nomes — manter _listaNovosHoje se já houver
                }
            }
            catch
            {
                novosClientes = totalHoje > totalOntem ? totalHoje - totalOntem : 0;
                novosLista = _listaNovosHoje;
            }

            // calcula retornados comparando cache anterior
            int retornadosCount = 0;
            List<ClientesHoje> retornadosLista = new();
            try
            {
                var prevJson = Preferences.Default.Get("lista_clientes_cache_prev", "");
                var prevList = !string.IsNullOrEmpty(prevJson)
                    ? JsonConvert.DeserializeObject<List<ClientesHoje>>(prevJson) ?? new List<ClientesHoje>()
                    : new List<ClientesHoje>();

                // mapa seguro (evita erro em caso de duplicatas no cache)
                var prevMap = prevList
                    .Where(p => !string.IsNullOrWhiteSpace(p.Cliente))
                    .GroupBy(p => p.Cliente!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // lista de nomes que são "retornados" (ativos agora e não ativos antes)
                var novosSet = new HashSet<string>(_listaNovosHoje ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

                var retornadosNomes = _listaCompletaServidor
                    .Where(x => string.Equals(x.Ativo?.Trim(), "ok", StringComparison.OrdinalIgnoreCase))
                    .Select(atual => (atual.Cliente ?? "").Trim())
                    .Where(nome =>
                        !string.IsNullOrEmpty(nome)
                        // somente se EXISTIA no cache anterior
                        && prevMap.TryGetValue(nome, out var antes)
                        // e antes não estava ativo
                        && !string.Equals(antes.Ativo?.Trim(), "ok", StringComparison.OrdinalIgnoreCase)
                        // e não foi detectado como novo no mesmo ciclo
                        && !novosSet.Contains(nome)
                    )
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // preservar/mesclar retornados persistidos ao invés de sobrescrever
                if (_listaRetornadosHoje == null || _listaRetornadosHoje.Count == 0)
                {
                    _listaRetornadosHoje = retornadosNomes;
                }
                else
                {
                    var set = new HashSet<string>(_listaRetornadosHoje, StringComparer.OrdinalIgnoreCase);
                    foreach (var n in retornadosNomes)
                    {
                        AddRetornado(n);
                    }
                }

                // sincroniza lista de objetos retornados para contagens e uso posterior
                var retornadosSetFinal = new HashSet<string>(_listaRetornadosHoje, StringComparer.OrdinalIgnoreCase);
                retornadosLista = _listaCompletaServidor
                    .Where(c => !string.IsNullOrWhiteSpace(c.Cliente) && retornadosSetFinal.Contains(c.Cliente.Trim()))
                    .ToList();

                retornadosCount = _listaRetornadosHoje.Count;
                // SalvarRetornadosNoDispositivo() já é chamado por AddRetornado quando necessário
                SalvarRetornadosNoDispositivo();
            }
            catch
            {
                retornadosCount = 0;
                retornadosLista = new List<ClientesHoje>();
            }

            // --- Persistir contagem de "RET. (HOJE)" para não zerar em atualizações subsequentes ---
            string hojeStr = DateTime.Now.ToString("yyyy-MM-dd");
            string chaveRetornoHoje = "retornados_hoje_count";
            string chaveRetornoHojeUltimo = "retornados_hoje_ultimo_dia";
            string ultimoDiaRetorno = Preferences.Default.Get(chaveRetornoHojeUltimo, "");

            if (ultimoDiaRetorno != hojeStr)
            {
                // primeiro cálculo do dia: armazena
                Preferences.Default.Set(chaveRetornoHoje, retornadosCount);
                Preferences.Default.Set(chaveRetornoHojeUltimo, hojeStr);
            }
            else
            {
                // dia já persistido: recupera o valor salvo para estabilidade durante o dia
                retornadosCount = Preferences.Default.Get(chaveRetornoHoje, retornadosCount);
            }

            // persiste "retornados do mês" evitando duplicar no mesmo dia
            string chaveMes = $"retornados_mes_{DateTime.Now:MM_yyyy}";
            string chaveMesUltimoDia = chaveMes + "_ultimo_dia";
            int retornadosMesPersistido = Preferences.Default.Get(chaveMes, 0);
            string ultimoDiaSalvo = Preferences.Default.Get(chaveMesUltimoDia, "");
            if (!string.IsNullOrEmpty(hojeStr) && ultimoDiaSalvo != hojeStr)
            {
                // soma os retornados detectados hoje ao acumulado mensal e marca o dia para evitar duplicação
                if (retornadosCount > 0)
                {
                    retornadosMesPersistido += retornadosCount;
                    Preferences.Default.Set(chaveMes, retornadosMesPersistido);
                }
                Preferences.Default.Set(chaveMesUltimoDia, hojeStr);
            }
            // valor exibido
            int retornadosDoMes = Preferences.Default.Get(chaveMes, 0);

            // conta retornados que estão fazendo a 1ª renovação (renovacoes == 1)
            int retornadosPrimeiraRenovacao = retornadosLista.Count(r => GetRenovacoesCount(r.Cliente) == 1);

            // soma retornados ao número de renovados exibido (conforme pedido)
            int renovadosComRetorno = renovadosCount + retornadosCount;

            // ... UI ...
            layout.Add(new Label { Text = "GESTÃO OPERACIONAL", FontSize = ObterFonteResponsiva("GESTÃO OPERACIONAL", 20, 16), FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#263238") });

            int totalOntemCount = Preferences.Default.Get("total_clientes_ontem", _listaCompletaServidor.Count);
            int totalAcoes = renovadosComRetorno + Math.Max(0, totalHoje - totalOntemCount);
            double progresso = Math.Min(1.0, (double)totalAcoes / 7.0);
            layout.Add(new ProgressBar { Progress = progresso, ProgressColor = totalAcoes >= 7 ? Color.FromArgb("#FFD54F") : Color.FromArgb("#1976D2"), HeightRequest = 14 });

            var gridStats = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() },
                RowDefinitions = { new RowDefinition(), new RowDefinition() },
                RowSpacing = 10,
                ColumnSpacing = 10
            };

            gridStats.Add(CriarCardStatus("PENDENTES", $"{pendentesCount}", Color.FromArgb("#E53935")), 0, 0);
            int faltamCount = Math.Max(0, _listaVenceHoje.Count - pausadosCount);
            gridStats.Add(CriarCardStatus("FALTAM", $"{faltamCount}", Color.FromArgb("#FFB300")), 1, 0);
            gridStats.Add(CriarCardStatus("PAUSADOS", $"{pausadosCount}", Color.FromArgb("#9E9E9E")), 2, 0);

            gridStats.Add(CriarCardStatus("ATIVOS", $"{_listaAtivosOk.Count}", Color.FromArgb("#1976D2")), 0, 1);
            gridStats.Add(CriarCardStatus("RENOVADOS", $"{renovadosComRetorno}", Color.FromArgb("#2E7D32")), 1, 1);
            gridStats.Add(CriarCardStatus("NOVOS", $"{novosClientes}", Color.FromArgb("#388E3C")), 2, 1);

            layout.Add(gridStats);

            // nova linha de retornados (apenas contagens, conforme solicitado)
            var gridRetornos = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition() },
                ColumnSpacing = 10,
                Margin = new Thickness(0, 8, 0, 0)
            };
            gridRetornos.Add(CriarCardStatus("RET. (HOJE)", $"{retornadosCount}", Color.FromArgb("#4CAF50")), 0, 0);
            gridRetornos.Add(CriarCardStatus("RET. (MÊS)", $"{retornadosDoMes}", Color.FromArgb("#00796B")), 1, 0);
            gridRetornos.Add(CriarCardStatus("1ª RENOVA", $"{retornadosPrimeiraRenovacao}", Color.FromArgb("#546E7A")), 2, 0);

            layout.Add(gridRetornos);

            // restante da UI inalterado (Entrada hoje, botões)
            double entradaHoje = _listaNfLocal.Sum(n => ParseValor(n.Valor));
            double custoTotal = _listaAtivosOk.Count * 11.0;
            double impostoHoje = entradaHoje * 0.06;
            double saldoFinal = entradaHoje - custoTotal - impostoHoje;

            // --- ADICIONADO: cálculo de lucroProjetadoMes para passar a AbrirFinancaPessoal ---
            double faturamentoTotal = GetTotalNfMesPersistido();
            double impostosTotal = Math.Round(faturamentoTotal * 0.06, 2);
            double custoAnunciosClientesPorDias = _listaAtivosOk.Count * 8.0; // aproximação usada na tela operacional
            double meuAnuncioTotalMes = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);
            double custoAnunciosTotal = custoAnunciosClientesPorDias + meuAnuncioTotalMes;
            double lucroProjetadoMes = Math.Round(faturamentoTotal - impostosTotal - custoAnunciosTotal, 2);



            // --- FIM DA ADIÇÃO ---

            var entradaCard = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(12) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 16,
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalOptions = LayoutOptions.Fill,
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = "ENTRADA HOJE", FontSize = ObterFonteResponsiva("ENTRADA HOJE", 16, 12), FontAttributes = FontAttributes.Bold, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center },
                        new Label { Text = $"R$ {entradaHoje:N2}", FontSize = ObterFonteResponsiva($"R$ {entradaHoje:N2}", 26, 16), FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#00C853"), HorizontalTextAlignment = TextAlignment.Center }
                    }
                }
            };
            layout.Add(entradaCard);

            layout.Add(CriarBotaoEstilizado("GESTÃO FINANCEIRA", Color.FromArgb("#2E7D32"), async () => await AbrirGestaoFinanceira()));
            layout.Add(CriarBotaoEstilizado("RELATÓRIO COMPLETO", Color.FromArgb("#1976D2"), async () =>
            {
                if (_listaNfLocal.Count == 0 && _listaRenovadosHoje.Count == 0 && _listaPausadosHoje.Count == 0)
                {
                    await DisplayAlert("Aviso", "Não há atividades para relatar.", "OK");
                    return;
                }

                string txtNf = _listaNfLocal.Count > 0 ? string.Join("\n", _listaNfLocal.Select(n => $"• {n.Cliente}: {n.Valor}")) : "Nenhuma NF gerada.";
                string txtRenovados = _listaRenovadosHoje.Count > 0 ? string.Join("\n", _listaRenovadosHoje.Select(r => $"✅ {r.Cliente} ({r.Plano})")) : "Nenhuma renovação registrada.";
                string txtPausados = _listaPausadosHoje.Count > 0 ? string.Join("\n", _listaPausadosHoje.Select(p => $"⛔ {p}")) : "Nenhum cliente pausado.";
                string txtNovos = _listaNovosHoje.Count > 0 ? string.Join("\n", _listaNovosHoje.Select(n => $"✨✅ {n}")) : "Nenhum cliente novo.";
                // construir texto de retornados (cole acima da criação de msg)
                string txtRetornados = _listaRetornadosHoje.Count > 0 ? string.Join("\n", _listaRetornadosHoje.Select(r => $"↩️✅ {r}")) : "Nenhum retorno.";
                string txtPendentesPagos = _listaPendentesPagos.Count > 0 ? string.Join("\n", _listaPendentesPagos.Select(p => $"💵 {p}")) : "Nenhum pendente pago.";


                // calcular uma vez, com proteção contra null
                double entradaTotal = (_listaNfLocal ?? Enumerable.Empty<NfModel>()).Sum(n => ParseValor(n.Valor));
                double custoPorDia = Preferences.Default.Get(KEY_CUSTO_POR_DIA, 8.0);
                double custoFixo = Math.Round(_listaAtivosOk.Count * custoPorDia, 2);
                double ImpostoDia = Math.Round(entradaTotal * 0.06, 2);
                double saldoLiquido = Math.Round(entradaTotal - custoFixo - ImpostoDia - custoMeuAnuncioHoje, 2);

                string msg = $"*📊 RELATÓRIO GERAL DO DIA*\n\n*💰 FINANCEIRO (NF):*\n{txtNf}\n\n*💵 PENDENTES PAGOS:*\n{txtPendentesPagos}\n\n*🔄 CLIENTES RENOVADOS:*\n{txtRenovados}\n\n*↩️🔄 CLIENTES RETORNADOS:*\n{txtRetornados}\n\n*✨🔄 NOVOS CLIENTES:*\n{txtNovos}\n\n*🚫 CLIENTES PAUSADOS:*\n{txtPausados}\n\n--- resumo ---\n*Entrada Total:* R$ {entradaTotal:N2}\n*Custo Fixo:* R$ {custoFixo:N2}\n*Imposto:* R$ {ImpostoDia:N2}\n*Anúncio Empresa:* R$ {custoMeuAnuncioHoje:N2}\n*Saldo Líquido:* R$ {saldoLiquido:N2}";
                string escolha = await DisplayActionSheet("Relatório — escolha ação", "Cancelar", null, "Visualizar", "Enviar");
                if (escolha == "Visualizar")
                {
                    await DisplayAlert("Relatório", msg, "OK");
                    return;
                }
                if (escolha == "Enviar")
                {
                    try
                    {
                        string url = $"https://api.whatsapp.com/send?text={Uri.EscapeDataString(msg)}";
                        await Launcher.Default.OpenAsync(new Uri(url));
                    }
                    catch
                    {
                        // fallback para Share se o launcher falhar
                        await Share.Default.RequestAsync(new ShareTextRequest { Title = "Relatório", Text = msg });
                    }

                    // somente após enviar, limpar listas e persistências
                    _listaNfLocal.Clear();
                    _listaRenovadosHoje.Clear();
                    _listaPausadosHoje.Clear();
                    _listaNovosHoje.Clear();
                    _listaRetornadosHoje.Clear();
                    _listaPendentesPagos.Clear();
                    _pendingHttpCommands.Clear();


                    SalvarPausadosNoDispositivo();
                    SalvarRenovadosNoDispositivo();
                    SalvarNovosNoDispositivo();
                    SalvarRetornadosNoDispositivo();
                    SalvarPendentesPagosNoDispositivo();
                    await SalvarPendingCommandsToPrefsAsync();




                    try { Preferences.Default.Remove("lista_nf_salva"); } catch { }

                    VoltarParaLista();
                }
            }, 13));

            // botão para ver / compartilhar variáveis


            layout.Add(CriarBotaoEstilizado("VOLTAR", Color.FromArgb("#9E9E9E"), VoltarParaLista));

            scroll.Content = layout;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(scroll);
                _floatBtn.IsVisible = false;
                _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
            });

        }
        private View CriarCardStatus(string titulo, string valor, Color cor)
        {
            string titleUpper = (titulo ?? "").ToUpperInvariant();
            var txtTitle = new Label
            {
                Text = titleUpper,
                FontSize = ObterFonteResponsiva(titleUpper, 13, 10),
                TextColor = Colors.Gray,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };
            var txtValue = new Label
            {
                Text = valor,
                FontSize = ObterFonteResponsiva(valor, 20, 12),
                FontAttributes = FontAttributes.Bold,
                TextColor = cor,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };

            return new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(12) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 12,
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = { txtTitle, txtValue }
                }
            };
        }
        private View CriarBotaoEstilizado(string t, Color c, Action a, int fontSize = 13)
        {
            double maxFont = Math.Max(16, fontSize);
            double minFont = 10;

            var textoLabel = new Label
            {
                Text = t,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontSize = ObterFonteResponsiva(t, maxFont, minFont),
                LineBreakMode = LineBreakMode.NoWrap, // força única linha
                MaxLines = 1,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var frame = new Frame
            {
                CornerRadius = 12,
                BackgroundColor = c,
                HasShadow = true,
                Padding = new Thickness(10, 8),
                MinimumWidthRequest = 72,
                Content = textoLabel,
                HorizontalOptions = LayoutOptions.Fill
            };

            // Ajusta fonte conforme a largura real do frame (corrige aparição de "...")
            frame.SizeChanged += (s, e) =>
            {
                try
                {
                    // corrige uso de propriedade inexistente: soma Left + Right
                    double horizontalPadding = frame.Padding.Left + frame.Padding.Right;
                    double avail = frame.Width - horizontalPadding;
                    if (avail <= 0) return;

                    // heurística para converter largura em tamanho de fonte
                    double factor = Math.Max(6.0, Math.Max(1, textoLabel.Text?.Length ?? 1) * 0.55);
                    double computed = Math.Round(avail / factor, 1);

                    textoLabel.FontSize = Math.Clamp(computed, minFont, maxFont);
                }
                catch
                {
                    textoLabel.FontSize = Math.Clamp((maxFont + minFont) / 2.0, minFont, maxFont);
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => a();
            frame.GestureRecognizers.Add(tap);
            return frame;
        }
        private double ObterFonteResponsiva(string texto, double maxFont = 16, double minFont = 10)
        {
            try
            {
                var info = Microsoft.Maui.Devices.DeviceDisplay.MainDisplayInfo;
                double widthDip = info.Width / info.Density;
                if (string.IsNullOrEmpty(texto)) return minFont * 1.08;
                // heurística simples: mais caracteres -> menor fonte, respeitando limites
                double factor = Math.Max(6.0, texto.Length * 0.55);
                double size = Math.Round(widthDip / factor, 1);

                // escala global leve (ex.: +8%) para aumentar todas as fontes mantendo proporções
                double escala = 1.08;
                return Math.Clamp(Math.Round(size * escala, 1), minFont * escala, maxFont * escala);
            }
            catch
            {
                double escala = 1.08;
                return Math.Clamp((maxFont + minFont) / 2.0 * escala, minFont * escala, maxFont * escala);
            }
        }
        private void ProcessarListas()
        {
            try
            {
                var hoje = DateTime.Now.Date;

                _listaAtivosOk = _listaCompletaServidor.Where(c => c.Ativo?.Trim().ToLower() == "ok").ToList();

                // agora inclui clientes cujo Fim seja igual a hoje ou antes (considera "dd/MM" e "dd/MM/yyyy")
                _listaVenceHoje = _listaCompletaServidor.Where(c =>
                {
                    if (c == null) return false;
                    if (c.Ativo?.Trim().ToLower() != "ok") return false;

                    var fimRaw = (c.Fim ?? "").Replace("\\", "").Trim();
                    if (string.IsNullOrEmpty(fimRaw)) return false;

                    // padroniza caso venha sem ano (ex: "05/03" -> "05/03/{anoAtual}")
                    string fimParaParse = fimRaw;
                    if (fimParaParse.Count(ch => ch == '/') == 1)
                        fimParaParse = fimParaParse + "/" + DateTime.Now.Year.ToString();

                    // tenta parse explícito no formato esperado
                    if (DateTime.TryParseExact(fimParaParse, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                    {
                        return dt.Date <= hoje;
                    }

                    // fallback: tenta parse genérico
                    if (DateTime.TryParse(fimParaParse, out DateTime dt2))
                    {
                        return dt2.Date <= hoje;
                    }

                    return false;
                }).ToList();

                _metaFixaDoDia = _listaVenceHoje.Count + _listaRenovadosHoje.Count;

                foreach (var c in _listaCompletaServidor)
                {
                    bool isAtivo = c.Ativo?.Trim().ToLower() == "ok";
                    bool temDadosPagamento = !string.IsNullOrWhiteSpace(c.Datapg) && !string.IsNullOrWhiteSpace(c.Pg);
                    if (isAtivo && temDadosPagamento)
                    {
                        if (!_listaPendentesLocal.Any(x => x.Cliente == c.Cliente))
                        {
                            c.IsPendente = true;
                            if (DateTime.TryParse(c.Datapg, out DateTime dataConvertida)) c.DataPagamentoPendente = dataConvertida;
                            else c.DataPagamentoPendente = DateTime.Now;
                            _listaPendentesLocal.Add(c);
                        }
                    }
                }

                SalvarPendentesNoDispositivo();
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () => { await DisplayAlert("Erro de Processamento", ex.Message, "OK"); });
            }
        }
        private void ExecutarBuscaReal()
        {
            string texto = _searchEntry.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(texto))
            {
                var resultadosGlobais = _listaCompletaServidor.Where(c => c.Cliente != null && c.Cliente.ToLower().Contains(texto)).ToList();
                ClientesExibidos.ReplaceRange(resultadosGlobais);
            }
            else
            {
                List<ClientesHoje> fonte = _modoAtual == "PENDENTES" ? _listaPendentesLocal : (_modoAtual == "HOJE" ? _listaVenceHoje : _listaAtivosOk);
                ClientesExibidos.ReplaceRange(fonte);
            }
        }
        private void OnBotaoHojeClicked() { _modoAtual = "HOJE"; _searchEntry.Text = string.Empty; ExecutarBuscaReal(); }
        private void OnBotaoPendentesClicked() { _modoAtual = "PENDENTES"; _searchEntry.Text = string.Empty; ExecutarBuscaReal(); }
        private void VoltarParaLista()
        {
            _containerConteudo.Children.Clear();
            _containerConteudo.Children.Add(_layoutPrincipal);
            _floatBtn.IsVisible = true;
            _floatBtnG.Margin = new Thickness(0, 0, 14, 78);
            ExecutarBuscaReal();
        }
        
        #endregion

        #region Tela Cliente
        private async Task AoTocarNoCliente(ClientesHoje c)
        {
            var scroll = new ScrollView { BackgroundColor = Colors.White };
            var detalhesStack = new VerticalStackLayout { Padding = 15, Spacing = 10, BackgroundColor = Colors.White };

            var lblNome = new Label
            {
                Text = c.Cliente.ToUpper(),
                FontSize = ObterFonteResponsiva(c.Cliente?.ToUpper() ?? "", 18, 14),
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Colors.Black
            };

            var tapNome = new TapGestureRecognizer();
            tapNome.Tapped += async (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(c.Cliente))
                    {
                        await Clipboard.Default.SetTextAsync(c.Cliente);
                        await ShowTemporaryNotification("Copiado");
                    }
                }
                catch
                {
                    // não falhar a UI em caso de erro no clipboard
                }
            };
            lblNome.GestureRecognizers.Add(tapNome);

            detalhesStack.Children.Add(lblNome);

            // cria stack de info (agora com contador de renovações)
            var infoStack = new VerticalStackLayout { Spacing = 4 };

            var lblPlano = new Label { Text = $"Plano Atual: {c.Plano}", FontSize = ObterFonteResponsiva($"Plano Atual: {c.Plano}", 13, 11), TextColor = Colors.Black };
            var lblInicio = new Label { Text = $"Ativação: {c.Inicio}", FontSize = ObterFonteResponsiva($"Ativação: {c.Inicio}", 13, 11), TextColor = Colors.Black };
            var lblFim = new Label { Text = $"Vencimento: {c.Fim}", FontSize = ObterFonteResponsiva($"Vencimento: {c.Fim}", 13, 11), TextColor = Colors.Black };
            var lblAtivo = new Label { Text = $"Ativo: {c.Ativo}", FontSize = ObterFonteResponsiva($"Ativo: {c.Ativo}", 13, 11), TextColor = Colors.Black };
            var lblSituacao = new Label { Text = $"Situação: {c.Situacao}", FontSize = ObterFonteResponsiva($"Situação: {c.Situacao}", 13, 11), TextColor = Colors.Black };
            var lblCel = new Label { Text = $"Celular: {FormatarTelefone(c.Cel)}", FontSize = ObterFonteResponsiva($"Celular: {FormatarTelefone(c.Cel)}", 13, 11), TextColor = Colors.Black };
            var lblCom = new Label { Text = $"Comercial: {FormatarTelefone(c.Celcon)}", FontSize = ObterFonteResponsiva($"Comercial: {FormatarTelefone(c.Celcon)}", 13, 11), FontAttributes = FontAttributes.Bold, TextColor = Colors.DarkGreen };

            // Label que mostra quantas renovações o cliente já fez
            var lblRenovacoes = new Label
            {
                Text = $"Renovações: {GetRenovacoesCount(c.Cliente)}",
                FontSize = ObterFonteResponsiva("Renovações: X", 13, 11),
                TextColor = Colors.DarkBlue
            };

            infoStack.Add(lblPlano);
            infoStack.Add(lblInicio);
            infoStack.Add(lblFim);
            infoStack.Add(lblAtivo);
            infoStack.Add(lblSituacao);
            infoStack.Add(lblCel);
            infoStack.Add(lblCom);
            infoStack.Add(lblRenovacoes); // adicionado contador de renovações

            var frameInfo = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                Background = Colors.White,
                Stroke = Colors.LightGray,
                Padding = 12,
                Content = infoStack
            };
            detalhesStack.Children.Add(frameInfo);

            detalhesStack.Children.Add(CriarBotaoEstilizado("EDITAR CADASTRO", Color.FromArgb("#1976D2"), async () => await AbrirEditarCliente(c)));

            var picker = new Picker { Title = "Clique para selecionar", ItemsSource = _planosDisponiveis, HeightRequest = 45, TextColor = Colors.Black };
            var lblPlanoEscolhido = new Label { Text = "", FontSize = ObterFonteResponsiva("Plano Escolhido", 16, 12), FontAttributes = FontAttributes.Bold, TextColor = Colors.RoyalBlue, HorizontalOptions = LayoutOptions.Center, IsVisible = false };
            picker.SelectedIndexChanged += (s, e) => { if (picker.SelectedIndex != -1) { lblPlanoEscolhido.Text = $"{picker.SelectedItem}"; lblPlanoEscolhido.IsVisible = true; } };

            detalhesStack.Children.Add(new Label { Text = "ESCOLHA O PLANO PARA RENOVAR", FontAttributes = FontAttributes.Bold, FontSize = ObterFonteResponsiva("ESCOLHA O PLANO PARA RENOVAR", 13, 11), TextColor = Colors.Gray });
            detalhesStack.Children.Add(lblPlanoEscolhido);
            detalhesStack.Children.Add(picker);

            var checkPendente = new CheckBox { IsChecked = c.IsPendente, Color = Colors.RoyalBlue };

            // Início (editable) — sugerir sempre a data de hoje como padrão; usuário pode alterar manualmente
            var dateInicioPicker = new DatePicker
            {
                Format = "dd/MM/yyyy",
                Date = DateTime.Now, // sugestão = hoje
                HeightRequest = 40,
                TextColor = Colors.Black
            };

            // Vencimento (editable apenas usado como datapg quando pendente) — padrão:
            // se cliente já tem Datapg válida, usa ela; senão tomorrow
            DateTime vencimentoDefault;
            if (!string.IsNullOrWhiteSpace(c.Datapg) && DateTime.TryParseExact(c.Datapg.Replace("\\", "").Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedPg))
                vencimentoDefault = parsedPg;
            else
                vencimentoDefault = DateTime.Now.AddDays(1);

            var dateVencimentoPicker = new DatePicker { Format = "dd/MM/yyyy", Date = vencimentoDefault, HeightRequest = 40, TextColor = Colors.Black, IsVisible = checkPendente.IsChecked };

            // toggle visibilidade do vencimento
            checkPendente.CheckedChanged += (s, e) =>
            {
                dateVencimentoPicker.IsVisible = e.Value;
                if (e.Value)
                {
                    if (string.IsNullOrWhiteSpace(c.Datapg))
                        dateVencimentoPicker.Date = DateTime.Now.AddDays(1);
                }
            };

            detalhesStack.Children.Add(new HorizontalStackLayout { Spacing = 5, Children = { checkPendente, new Label { Text = "Pagamento Pendente?", FontSize = ObterFonteResponsiva("Pagamento Pendente?", 13, 11), VerticalOptions = LayoutOptions.Center, TextColor = Colors.Black } } });
            detalhesStack.Children.Add(new Label { Text = "Data de Início:", FontSize = ObterFonteResponsiva("Data de Início:", 13, 11), TextColor = Colors.Gray });
            detalhesStack.Children.Add(dateInicioPicker);
            detalhesStack.Children.Add(new Label { Text = "Data de Vencimento (pagamento):", FontSize = ObterFonteResponsiva("Data de Vencimento (pagamento):", 13, 11), TextColor = Colors.Gray });
            detalhesStack.Children.Add(dateVencimentoPicker);

            detalhesStack.Children.Add(CriarBotaoEstilizado("CONFIRMAR RENOVAÇÃO", Colors.Green, async () =>
            {
                if (picker.SelectedIndex == -1)
                {
                    await DisplayAlert("Atenção", "Selecione um plano!", "OK");
                    return;
                }

                string planoEscolhido = picker.SelectedItem?.ToString() ?? "";
                string planoLimpo = LimparNomePlano(planoEscolhido);
                bool pend = checkPendente.IsChecked;

                DateTime inicioSelecionado = dateInicioPicker.Date.Value;
                int diasDoPlano = ObterDiasPorPlano(planoEscolhido);
                DateTime fimParaPlanilha = inicioSelecionado.AddDays(diasDoPlano);

                // --- solicitar valor se pendente ---
                string valorPrompt = "R$ 0,00";
                DateTime? datapg = null;

                if (pend)
                {
                    datapg = dateVencimentoPicker.Date;

                    string sugestao = !string.IsNullOrWhiteSpace(c.Pg) ? c.Pg : $"R$ {ObterValorPorNomePlano(planoEscolhido):N2}";
                    var input = await DisplayPromptAsync("Valor da dívida", "Insira o valor da dívida (ex: 150,00):", "OK", "Cancelar", sugestao, -1, Keyboard.Numeric);
                    if (input == null) return;

                    double parsed = ParseValor(input);
                    if (parsed <= 0) parsed = ObterValorPorNomePlano(planoEscolhido);
                    valorPrompt = $"R$ {parsed:N2}";
                }

                string valorFinal = ValorOuPlano(valorPrompt, planoEscolhido);

                await ExecutarComLoader(async () =>
                {
                    await EnviarDadosRenovacao(c.Cliente, planoLimpo, pend, inicioSelecionado, datapg, fimParaPlanilha, valorPrompt);

                    // --- só gera NF se NÃO for pendente ---
                    if (!pend)
                    {
                        var nf = new NfModel
                        {
                            Data = inicioSelecionado.ToString("dd/MM/yyyy"),
                            Cliente = c.Cliente,
                            Valor = valorFinal,
                            Plano = planoEscolhido,
                            Dias = diasDoPlano
                        };
                        _listaNfLocal.Add(nf);
                        Preferences.Default.Set("lista_nf_salva", JsonConvert.SerializeObject(_listaNfLocal));
                        AddToTotalNfMes(ParseValor(valorFinal));

                        // acumula custo de anúncios
                        double custoPorDia = Preferences.Default.Get(KEY_CUSTO_POR_DIA, 8.0);
                        double incrementoCustoAnuncio = diasDoPlano * custoPorDia;
                        double acumuladoAtual = Preferences.Default.Get(KEY_CUSTO_ANUNCIOS_MES, 0.0);
                        Preferences.Default.Set(KEY_CUSTO_ANUNCIOS_MES, acumuladoAtual + incrementoCustoAnuncio);
                    }

                    // Atualiza cliente
                    c.Plano = planoEscolhido;
                    c.Inicio = inicioSelecionado.ToString("dd/MM/yyyy");
                    c.Fim = fimParaPlanilha.ToString("dd/MM/yyyy");
                    c.IsPendente = pend;
                    c.Datapg = pend ? datapg?.ToString("dd/MM/yyyy") ?? "" : "";
                    c.Pg = pend ? valorPrompt : "";
                    c.DataPagamentoPendente = pend ? datapg ?? DateTime.Now : DateTime.MinValue;

                    if (pend)
                    {
                        var existente = _listaPendentesLocal.FirstOrDefault(x => string.Equals(x.Cliente, c.Cliente, StringComparison.OrdinalIgnoreCase));
                        if (existente == null) _listaPendentesLocal.Add(c);
                        else
                        {
                            existente.IsPendente = true;
                            existente.Datapg = c.Datapg;
                            existente.Pg = c.Pg;
                            existente.DataPagamentoPendente = c.DataPagamentoPendente;
                            existente.Inicio = c.Inicio;
                            existente.Fim = c.Fim;
                        }
                        SalvarPendentesNoDispositivo();
                    }
                    else
                    {
                        _listaPendentesLocal.RemoveAll(x => string.Equals(x.Cliente, c.Cliente, StringComparison.OrdinalIgnoreCase));
                        SalvarPendentesNoDispositivo();
                    }

                    // Decisão: renovado ou retornado
                    if (string.Equals(c.Ativo?.Trim(), "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        AddRenovado(c);
                        IncrementRenovacoesCount(c.Cliente);
                        lblRenovacoes.Text = $"Renovações: {GetRenovacoesCount(c.Cliente)}";
                    }
                    else
                    {
                        AddRetornado(c.Cliente);
                    }

                    ProcessarListas();
                    AtualizarDashboardFinanceiro();
                    SalvarVistosLocais();

                    _listaVenceHoje.RemoveAll(x => string.Equals(x.Cliente, c.Cliente, StringComparison.OrdinalIgnoreCase));
                    if (_modoAtual == "HOJE") MainThread.BeginInvokeOnMainThread(() => ExecutarBuscaReal());
                });

                await DisplayAlert("Sucesso", "Renovação registrada e enviada.", "OK");
                VoltarParaLista();
            }));


            var gridWhats = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 8 };
            gridWhats.Add(CriarBotaoEstilizado("WHATS PESSOAL", Color.FromArgb("#25D366"), async () => await AbrirWhatsApp(c.Cel, _listaVenceHoje.Contains(c), c)), 0);
            gridWhats.Add(CriarBotaoEstilizado("WHATS COMERCIAL", Color.FromArgb("#128C7E"), async () => await AbrirWhatsApp(c.Celcon, _listaVenceHoje.Contains(c), c)), 1);

            detalhesStack.Children.Add(gridWhats);

            detalhesStack.Children.Add(CriarBotaoEstilizado("PAUSAR CLIENTE", Colors.Red, async () =>
            {
                bool confirm = await DisplayAlert("Pausar", $"Deseja pausar {c.Cliente}?", "Sim", "Não");
                if (confirm)
                {
                    await ExecutarComLoader(async () =>
                    {
                        await PausarClienteNaPlanilha(c.Cliente);
                        if (!_listaPausadosHoje.Contains(c.Cliente)) _listaPausadosHoje.Add(c.Cliente);
                        SalvarPausadosNoDispositivo();
                        _listaAtivosOk.RemoveAll(x => x.Cliente == c.Cliente);
                        _listaVenceHoje.RemoveAll(x => x.Cliente == c.Cliente);
                    });
                    VoltarParaLista();
                }
                else
                {

                }
            }));

            detalhesStack.Children.Add(CriarBotaoEstilizado("VOLTAR", Colors.Gray, async () =>
            {
                try
                {
                    string pgOriginal = c.Pg ?? "";
                    bool estavaPendente = c.IsPendente;
                    double pgValor = ParseValor(pgOriginal);

                    // Se havia dívida e o usuário desmarcou o checkbox (ou simplesmente existia dívida e está voltando),
                    // limpar pendência na planilha, remover da lista de pendentes e criar NF local.
                    if (estavaPendente && (!checkPendente.IsChecked))
                    {
                        await ExecutarComLoader(async () =>
                        {
                            // 1) limpa pendência na planilha (remoto)
                            await LimparPendenciaNaPlanilha(c.Cliente);

                            // 2) atualiza estado local do cliente e lista de pendentes
                            c.IsPendente = false;
                            c.Datapg = "";
                            c.Pg = "";
                            _listaPendentesLocal.RemoveAll(x => string.Equals(x.Cliente, c.Cliente, StringComparison.OrdinalIgnoreCase));
                            SalvarPendentesNoDispositivo();

                            if (pgValor > 0)
                            {
                                string valorAj = AjustarValorNf(pgOriginal);
                                string hoje = DateTime.Now.ToString("dd/MM/yyyy");
                                string valorParaComparar = ValorOuPlano(valorAj, c.Plano);

                                bool existe = _listaNfLocal.Any(n =>
                                string.Equals(n.Cliente?.Trim(), c.Cliente?.Trim(), StringComparison.OrdinalIgnoreCase)
                                && string.Equals(n.Data?.Trim(), hoje, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(n.Valor?.Trim(), valorParaComparar?.Trim(), StringComparison.OrdinalIgnoreCase));

                                // 3) cria NF local se não existir (evita duplicados)

                                if (!existe)
                                {
                                    _listaNfLocal.Add(new NfModel
                                    {
                                        Data = DateTime.Now.ToString("dd/MM/yyyy"),
                                        Cliente = c.Cliente,
                                        Valor = ValorOuPlano(valorAj, c.Plano),
                                        Plano = c.Plano,
                                        Dias = ObterDiasPorPlano(c.Plano)
                                    });
                                    // marca como pendente pago para aparecer no relatório
                                    AddPendentePago(c.Cliente);
                                    Preferences.Default.Set("lista_nf_salva", JsonConvert.SerializeObject(_listaNfLocal));
                                    AddToTotalNfMes(ParseValor(ValorOuPlano(valorAj, c.Plano)));
                                }
                            }

                            // 2.a) atualiza cache local (_listaCompletaServidor) para evitar que "ATUALIZAR" recarregue pendência antiga
                            try
                            {
                                var servidor = _listaCompletaServidor.FirstOrDefault(x => string.Equals(x.Cliente, c.Cliente, StringComparison.OrdinalIgnoreCase));
                                if (servidor != null)
                                {
                                    servidor.IsPendente = false;
                                    servidor.Datapg = "";
                                    servidor.Pg = "";
                                }
                                Preferences.Default.Set("lista_clientes_cache", JsonConvert.SerializeObject(_listaCompletaServidor));
                            }
                            catch
                            {
                                // não falhar se persistência der problema
                            }

                            // 4) atualiza listas/UI
                            ProcessarListas();
                            AtualizarDashboardFinanceiro();
                            SalvarVistosLocais();
                        });
                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Erro", ex.Message, "OK");
                }

                VoltarParaLista();
            }));

            scroll.Content = detalhesStack;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(scroll);
                _floatBtn.IsVisible = false;
                _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
            });
        }
        private async Task AbrirEditarCliente(ClientesHoje c)
        {
            // guarda nome original (ajuda a localizar registro se nome for alterado)
            string originalName = c.Cliente ?? "";

            var s = c.Celcon ?? "";
            // remove quaisquer caracteres não-numéricos antes de aplicar a máscara simples
            s = Regex.Replace(s, @"[^\d]", "");
            if (s.Length > 2) s = s.Insert(2, " ");
            if (s.Length > 8) s = s.Insert(8, "-");


            //var bg = Colors.White;
            var bg = Color.FromArgb("#F6F8FB"); // fundo levemente acinzentado para contraste
            var layout = new VerticalStackLayout { Padding = 14, Spacing = 12, BackgroundColor = bg };

            // helper local para criar campo com label e borda visível


            layout.Add(new Label { Text = "EDITAR CADASTRO", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black, HorizontalOptions = LayoutOptions.Center });

            var entryNome = new Entry { Text = c.Cliente, Placeholder = "Nome", TextColor = Colors.Black };
            var entryPlano = new Entry { Text = c.Plano, Placeholder = "Plano", TextColor = Colors.Black };
            var entryInicio = new Entry { Text = c.Inicio, Placeholder = "Início (dd/MM/yyyy)", TextColor = Colors.Black };
            var entryFim = new Entry { Text = c.Fim, Placeholder = "Fim (dd/MM/yyyy)", TextColor = Colors.Black };
            var entrySituacao = new Entry { Text = c.Situacao, Placeholder = "Situação", TextColor = Colors.Black };
            var entryCel = new Entry { Text = c.Cel, Placeholder = "Celular", Keyboard = Keyboard.Telephone, TextColor = Colors.Black };
            var entryCelcon = new Entry { Text = s, Placeholder = "Comercial", Keyboard = Keyboard.Telephone, TextColor = Colors.Black };
            var entryAtivo = new Entry { Text = c.Ativo, Placeholder = "Ativo (OK/NÃO)", TextColor = Colors.Black };
            var entryDatapg = new Entry { Text = c.Datapg, Placeholder = "Data PG (dd/MM/yyyy)", TextColor = Colors.Black };
            var entryPg = new Entry { Text = c.Pg, Placeholder = "Valor PG (R$)", Keyboard = Keyboard.Numeric, TextColor = Colors.Black };



            layout.Add(entryNome);

            var gridName = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8, BackgroundColor = Colors.White };
            gridName.Add(new VerticalStackLayout { Children = { new Label { Text = "Plano", FontSize = 17, TextColor = Colors.Gray }, entryPlano } }, 0, 0);
            gridName.Add(new VerticalStackLayout { Children = { new Label { Text = "Situação", FontSize = 17, TextColor = Colors.Gray }, entrySituacao } }, 1, 0);
            layout.Add(gridName);

            var gridDates = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8, BackgroundColor = Colors.White };
            gridDates.Add(new VerticalStackLayout { Children = { new Label { Text = "Início", FontSize = 17, TextColor = Colors.Gray }, entryInicio } }, 0, 0);
            gridDates.Add(new VerticalStackLayout { Children = { new Label { Text = "Fim", FontSize = 17, TextColor = Colors.Gray }, entryFim } }, 1, 0);
            layout.Add(gridDates);


            layout.Add(new HorizontalStackLayout { Spacing = 12, Children = { new VerticalStackLayout { Children = { new Label { Text = "Celular", FontSize = 17, TextColor = Colors.Gray, BackgroundColor = Colors.White }, entryCel } }, new VerticalStackLayout { Children = { new Label { Text = "Comercial", FontSize = 17, TextColor = Colors.Gray, BackgroundColor = Colors.White }, entryCelcon } }, new VerticalStackLayout { Children = { new Label { Text = "Ativo", FontSize = 17, TextColor = Colors.Gray, BackgroundColor = Colors.White }, entryAtivo } } } });
            // layout.Add(entryAtivo);

            var gridPg = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8, BackgroundColor = Colors.White };
            gridPg.Add(new VerticalStackLayout { Children = { new Label { Text = "Data PG", FontSize = 17, TextColor = Colors.Gray }, entryDatapg } }, 0, 0);
            gridPg.Add(new VerticalStackLayout { Children = { new Label { Text = "Valor PG", FontSize = 17, TextColor = Colors.Gray }, entryPg } }, 1, 0);
            layout.Add(gridPg);



            var btns = new VerticalStackLayout { Spacing = 8 };
            // Substitua apenas o bloco do botão "SALVAR" dentro de AbrirEditarCliente por este:
            // Substitua o bloco do botão "SALVAR" dentro de AbrirEditarCliente por este (uso de EnviarEdicaoClienteAsync)
            // Substitua o bloco do botão "SALVAR" dentro de AbrirEditarCliente por este:
            btns.Add(CriarBotaoEstilizado("SALVAR", Color.FromArgb("#2E7D32"), async () =>
            {
                // monta o corpo com TODOS os campos editáveis (envia sempre todos)
                var corpo = new Dictionary<string, string>
                {
                    { "cliente_original", originalName },
                    { "cliente", entryNome.Text?.Trim() ?? "" },
                    { "plano", entryPlano.Text?.Trim() ?? "" },
                    { "inicio", entryInicio.Text?.Trim() ?? "" },
                    { "fim", entryFim.Text?.Trim() ?? "" },
                    { "situacao", entrySituacao.Text?.Trim() ?? "" },
                    { "ativo", entryAtivo.Text?.Trim() ?? "" },
                    { "datapg", entryDatapg.Text?.Trim() ?? "" },
                    { "pg", entryPg.Text?.Trim() ?? "" },
                    { "cel", entryCel.Text?.Trim() ?? "" },
                    { "celcon", entryCelcon.Text?.Trim() ?? "" }

                };

                const string url = "https://kflmulti.com/AndroidStudio/AlteraPlanilha.php";

                bool enviado = false;

                // Executa o POST mostrando o loader (ExecutarComLoader já manipula _loader)
                await ExecutarComLoader(async () =>
                {

                    // usa o helper central que faz POST form-url-encoded e enfileira em caso de falha
                    bool enviado = await PostFormToPlanilhaAsync(url, corpo, operation: "edit", localRef: originalName, showAlert: true);

                    // pequena espera para melhorar percepção da animação (opcional)
                    await Task.Delay(120);
                });

                if (enviado)
                    await DisplayAlert("Sucesso", "Alterações aplicadas na planilha.", "OK");
                else
                    await DisplayAlert("Aviso", "Não foi possível confirmar a alteração agora. Operação enfileirada.", "OK");

                // volta para lista sempre recarregando do servidor (fonte de verdade)
                VoltarParaLista();
                await CarregarDadosServidor();
            }));


            btns.Add(CriarBotaoEstilizado("CANCELAR", Color.FromArgb("#9E9E9E"), () =>
            {
                // voltar à tela de detalhes sem salvar
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(_layoutPrincipal);
                _floatBtn.IsVisible = true;
                ExecutarBuscaReal();
            }));

            layout.Add(btns);

            var scroll = new ScrollView { BackgroundColor = bg, Content = layout };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(scroll);
                _floatBtn.IsVisible = false;
                _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
            });
        }
        private string FormatarTelefone(string numero) =>
            Regex.Replace(numero ?? "", @"[^\d]", "").Length == 11
                ? Regex.Replace(Regex.Replace(numero ?? "", @"[^\d]", ""), @"(\d{2})(\d{5})(\d{4})", "($1) $2-$3")
                : (numero ?? "");
        private async Task AbrirWhatsApp(string numero, bool incluirMensagem, ClientesHoje cliente)
        {
            var num = Regex.Replace(numero ?? "", @"[^\d]", "");
            if (string.IsNullOrEmpty(num)) return;
            cliente.ContatoFeitoHoje = true;
            SalvarVistosLocais();
            string url = $"https://wa.me/55{num}";
            bool confirm = await DisplayAlert("Notificar Renovação", "Deseja notificar ou confirmar?", "Notificar", "Confirmar");
            if (confirm)
            {
                if (incluirMensagem) url += $"?text={Uri.EscapeDataString("Confirma por gentileza para eu poder fazer a configuração de renovação para seu anúncio não pausar")}";
                await Launcher.Default.OpenAsync(url);
            }
            else
            {
                if (incluirMensagem) url += $"?text={Uri.EscapeDataString("Olá... A renovação do seu plano é hoje, gostaria de saber se vai renovar, se vai manter o mesmo plano ou se vai alterar... O contato é feito para não deixar o anúncio pausar pois a pausa do anúncio atrapalha o aprendizado do algoritmo para melhorar cada vez mais")}";
                await Launcher.Default.OpenAsync(url);
            }
            VoltarParaLista();
        }
        private void SalvarVistosLocais()
        {
            var json = JsonConvert.SerializeObject(_listaCompletaServidor);
            Preferences.Default.Set("backup_vistos_hoje", json);
            Preferences.Default.Set("data_vistos", DateTime.Now.ToShortDateString());
        }
        private async Task CarregarDadosServidor()
        {
            try
            {
                await TryEnviarComandosPendentesAsync();
            }
            catch
            {
                // não bloquear fluxo se falhar
            }
            // 1) Carrega cache local (se existir) e atualiza a UI imediatamente
            try
            {
                var jsonCache = Preferences.Default.Get("lista_clientes_cache", "");
                if (!string.IsNullOrEmpty(jsonCache))
                {
                    try
                    {
                        _listaCompletaServidor = JsonConvert.DeserializeObject<List<ClientesHoje>>(jsonCache) ?? new List<ClientesHoje>();
                        ProcessarListas();
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _modoAtual = "ATIVOS";
                            _searchEntry.Text = string.Empty;
                            ExecutarBuscaReal();
                        });
                    }
                    catch
                    {
                        // Se cache corrompido, ignora e segue para tentativa HTTP
                        _listaCompletaServidor = new List<ClientesHoje>();
                    }
                }
            }
            catch
            {
                // não falhar se Preferences der problema
            }

            // 2) Tenta atualizar via HTTP; se falhar, mantém o que já foi carregado do cache
            try
            {
                MainThread.BeginInvokeOnMainThread(() => { _loader.IsVisible = true; _loader.IsRunning = true; _listView.Opacity = 0.3; });

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var res = await client.GetStringAsync("https://kflmulti.com/AndroidStudio/BuscaClientes.php");

                if (res.Contains("[")) res = res.Substring(res.IndexOf("["));

                var listaServidor = JsonConvert.DeserializeObject<List<ClientesHoje>>(res) ?? new List<ClientesHoje>();

                // --- LÓGICA DE RESET DOS VISTOS ---
                string dataUltimoContato = Preferences.Default.Get("data_vistos", "");
                string hoje = DateTime.Now.ToShortDateString();

                if (dataUltimoContato != hoje)
                {
                    foreach (var cliente in listaServidor) cliente.ContatoFeitoHoje = false;
                    Preferences.Default.Remove("backup_vistos_hoje");
                    Preferences.Default.Set("data_vistos", hoje);
                }
                else
                {
                    var jsonVistos = Preferences.Default.Get("backup_vistos_hoje", "");
                    if (!string.IsNullOrEmpty(jsonVistos))
                    {
                        var listaComVistos = JsonConvert.DeserializeObject<List<ClientesHoje>>(jsonVistos);
                        foreach (var c in listaServidor)
                        {
                            var correspondente = listaComVistos.FirstOrDefault(x => x.Cliente == c.Cliente);
                            if (correspondente != null)
                                c.ContatoFeitoHoje = correspondente.ContatoFeitoHoje;
                        }
                    }
                }

                // Substitui lista em memória e persiste cache local

                _listaCompletaServidor = listaServidor;
                try
                {
                    await TryEnviarComandosPendentesAsync();
                }
                catch
                {
                    // ignora
                }

                try
                {
                    // salva o cache antigo em 'lista_clientes_cache_prev' antes de sobrescrever
                    var oldCache = Preferences.Default.Get("lista_clientes_cache", "");
                    if (!string.IsNullOrEmpty(oldCache))
                    {
                        Preferences.Default.Set("lista_clientes_cache_prev", oldCache);
                    }

                    Preferences.Default.Set("lista_clientes_cache", JsonConvert.SerializeObject(_listaCompletaServidor));
                }
                catch
                {
                    // se persistência falhar, não quebra o fluxo
                }

                // Regras de meta/contagem e listas dependentes
                DateTime ultimaAtualizacao = Preferences.Default.Get("data_ultima_meta", DateTime.MinValue);
                if (DateTime.Now.Date > ultimaAtualizacao.Date)
                {
                    Preferences.Default.Set("total_clientes_ontem", _listaCompletaServidor.Count);
                    Preferences.Default.Set("data_ultima_meta", DateTime.Now.Date);
                }

                ProcessarListas();


                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _modoAtual = "ATIVOS";
                    _searchEntry.Text = string.Empty;
                    ExecutarBuscaReal();
                });
            }
            catch (Exception)
            {
                // Aviso simples: não interrompe fluxo, continua com cache já carregado (se houver)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Aviso", "Não foi possível atualizar. Usando última lista disponível (offline).", "OK");
                });
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => { _loader.IsRunning = false; _loader.IsVisible = false; _listView.Opacity = 1.0; });
            }
        }
        private void SalvarPausadosNoDispositivo()
        {
            try { Preferences.Default.Set(KEY_PAUSADOS_HOJE, JsonConvert.SerializeObject(_listaPausadosHoje)); }
            catch { /* não falhar a UI */ }
        }
        private void CarregarPausadosDoDispositivo()
        {
            try
            {
                var json = Preferences.Default.Get(KEY_PAUSADOS_HOJE, "");
                if (!string.IsNullOrEmpty(json)) _listaPausadosHoje = JsonConvert.DeserializeObject<List<string>>(json) ?? new();
            }
            catch { _listaPausadosHoje = new List<string>(); }
        }
        private void SalvarRenovadosNoDispositivo()
        {
            try
            {
                var nomes = _listaRenovadosHoje?.Select(r => r.Cliente).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
                Preferences.Default.Set(KEY_RENOVADOS_HOJE, JsonConvert.SerializeObject(nomes));
            }
            catch
            {
                // não falhar a UI
            }
        }
        private void CarregarRenovadosDoDispositivo()
        {
            try
            {
                var json = Preferences.Default.Get(KEY_RENOVADOS_HOJE, "");
                var nomes = !string.IsNullOrEmpty(json) ? JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>() : new List<string>();

                // ler retornados persistidos para filtrar
                var jsonRet = Preferences.Default.Get(KEY_RETORNADOS_HOJE, "");
                var retornadosPersistidos = !string.IsNullOrEmpty(jsonRet) ? JsonConvert.DeserializeObject<List<string>>(jsonRet) ?? new List<string>() : new List<string>();
                var retornadosSet = new HashSet<string>(retornadosPersistidos.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);

                var lista = new List<ClientesHoje>();
                foreach (var n in nomes)
                {
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    var t = n.Trim();

                    // se está entre os retornados persistidos, pular (não carregar como renovado)
                    if (retornadosSet.Contains(t)) continue;

                    // tenta reaproveitar referência do cache do servidor
                    var existente = _listaCompletaServidor.FirstOrDefault(x => string.Equals(x.Cliente?.Trim(), t, StringComparison.OrdinalIgnoreCase));
                    if (existente != null)
                        lista.Add(existente);
                    else
                        // mantém pelo menos o nome para aparecer no relatório
                        lista.Add(new ClientesHoje { Cliente = t });
                }

                _listaRenovadosHoje = lista;

                // garantir persistência coerente (remover nomes que filtramos)
                try
                {
                    var persistedFiltered = _listaRenovadosHoje.Select(r => r.Cliente).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    Preferences.Default.Set(KEY_RENOVADOS_HOJE, JsonConvert.SerializeObject(persistedFiltered));
                }
                catch { /* ignorar falha de persistência */ }
            }
            catch
            {
                _listaRenovadosHoje = new List<ClientesHoje>();
            }
        }
        private void SalvarNovosNoDispositivo()
        {
            try { Preferences.Default.Set(KEY_NOVOS_HOJE, JsonConvert.SerializeObject(_listaNovosHoje)); }
            catch { /* não falhar a UI */ }
        }
        private void CarregarNovosDoDispositivo()
        {
            try
            {
                var json = Preferences.Default.Get(KEY_NOVOS_HOJE, "");
                if (!string.IsNullOrEmpty(json)) _listaNovosHoje = JsonConvert.DeserializeObject<List<string>>(json) ?? new();
            }
            catch { _listaNovosHoje = new List<string>(); }
        }
        private void SalvarRetornadosNoDispositivo()
        {
            try { Preferences.Default.Set(KEY_RETORNADOS_HOJE, JsonConvert.SerializeObject(_listaRetornadosHoje)); }
            catch { /* não falhar a UI */ }
        }
        private void AddRetornado(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return;
            try
            {
                var trimmed = nome.Trim();

                if (_listaRetornadosHoje == null) _listaRetornadosHoje = new List<string>();
                if (_listaRenovadosHoje == null) _listaRenovadosHoje = new List<ClientesHoje>();

                // remover da lista de renovados em memória
                _listaRenovadosHoje.RemoveAll(r => string.Equals(r.Cliente?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

                // também atualizar persistência de renovados para evitar recarregar depois
                try
                {
                    var jsonRen = Preferences.Default.Get(KEY_RENOVADOS_HOJE, "");
                    if (!string.IsNullOrEmpty(jsonRen))
                    {
                        var renNomes = JsonConvert.DeserializeObject<List<string>>(jsonRen) ?? new List<string>();
                        var newRen = renNomes.Where(n => !string.Equals(n?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
                        Preferences.Default.Set(KEY_RENOVADOS_HOJE, JsonConvert.SerializeObject(newRen));
                    }
                }
                catch { /* ignorar falha de persistência */ }

                // adicionar retornado se não existir
                if (!_listaRetornadosHoje.Any(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    _listaRetornadosHoje.Add(trimmed);
                    SalvarRetornadosNoDispositivo();

                    // Atualiza contador diário persistido (retornados_hoje_count / retornados_hoje_ultimo_dia)
                    try
                    {
                        string hojeStr = DateTime.Now.ToString("yyyy-MM-dd");
                        string chaveRetornoHoje = "retornados_hoje_count";
                        string chaveRetornoHojeUltimo = "retornados_hoje_ultimo_dia";
                        string ultimoDiaRetorno = Preferences.Default.Get(chaveRetornoHojeUltimo, "");

                        if (ultimoDiaRetorno != hojeStr)
                        {
                            Preferences.Default.Set(chaveRetornoHoje, 1);
                            Preferences.Default.Set(chaveRetornoHojeUltimo, hojeStr);
                        }
                        else
                        {
                            int atual = Preferences.Default.Get(chaveRetornoHoje, 0);
                            Preferences.Default.Set(chaveRetornoHoje, atual + 1);
                        }
                    }
                    catch
                    {
                        // não falhar fluxo principal se persistência der problema
                    }
                }

                // atualizar dashboard/UI imediatamente
                ProcessarListas();
                AtualizarDashboardFinanceiro();
            }
            catch
            {
                // não falhar fluxo principal
            }
        }
        private void CarregarRetornadosDoDispositivo()
        {
            try
            {
                var json = Preferences.Default.Get(KEY_RETORNADOS_HOJE, "");
                if (string.IsNullOrEmpty(json))
                {
                    _listaRetornadosHoje = new List<string>();
                    return;
                }

                var nomes = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _listaRetornadosHoje = new List<string>();
                foreach (var n in nomes)
                {
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    var t = n.Trim();

                    if (!set.Contains(t))
                    {
                        set.Add(t);
                        _listaRetornadosHoje.Add(t);
                    }
                }

                // remove quaisquer entradas correspondentes em renovados (memória) e persiste renovados filtrados
                try
                {
                    if (_listaRenovadosHoje != null && _listaRenovadosHoje.Count > 0)
                    {
                        _listaRenovadosHoje.RemoveAll(r => !string.IsNullOrWhiteSpace(r.Cliente) && set.Contains(r.Cliente.Trim()));
                        // persistir renovados atualizados
                        SalvarRenovadosNoDispositivo();
                    }
                }
                catch { /* não falhar se ocorrer problema */ }
            }
            catch
            {
                _listaRetornadosHoje = new List<string>();
            }
        }
        private void AddRenovado(ClientesHoje cliente)
        {
            if (cliente == null || string.IsNullOrWhiteSpace(cliente.Cliente)) return;
            try
            {
                var trimmed = cliente.Cliente.Trim();

                // carrega retornados persistidos rapidamente para checagem
                var jsonRet = Preferences.Default.Get(KEY_RETORNADOS_HOJE, "");
                var retornadosPersistidos = !string.IsNullOrEmpty(jsonRet) ? JsonConvert.DeserializeObject<List<string>>(jsonRet) ?? new List<string>() : new List<string>();
                var retornadosSet = new HashSet<string>(retornadosPersistidos.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);

                // também checa lista em memória
                if ((_listaRetornadosHoje != null && _listaRetornadosHoje.Any(r => string.Equals(r.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
                    || retornadosSet.Contains(trimmed))
                {
                    // se for retornado, não adicionar aos renovados
                    return;
                }

                if (_listaRenovadosHoje == null) _listaRenovadosHoje = new List<ClientesHoje>();

                var idx = _listaRenovadosHoje.FindIndex(x => string.Equals(x.Cliente?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    _listaRenovadosHoje[idx] = cliente;
                else
                    _listaRenovadosHoje.Add(cliente);

                // persiste nomes
                SalvarRenovadosNoDispositivo();

                // persistir contador do dia para estabilidade
                string hojeStr = DateTime.Now.ToString("yyyy-MM-dd");
                Preferences.Default.Set("renovados_hoje_count", _listaRenovadosHoje.Count);
                Preferences.Default.Set("renovados_hoje_ultimo_dia", hojeStr);

                // atualizar dashboard e demais estados imediatamente
                ProcessarListas();
                AtualizarDashboardFinanceiro();
            }
            catch
            {
                // não falhar fluxo principal
            }
        }
        private void AddNovo(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return;
            var trimmed = nome.Trim();

            if (_listaNovosHoje == null) _listaNovosHoje = new List<string>();

            // evitar duplicatas (case-insensitive)
            if (_listaNovosHoje.Any(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase))) return;

            _listaNovosHoje.Add(trimmed);
            SalvarNovosNoDispositivo();

            // atualizar UI/contagens imediatamente
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProcessarListas();
                    AtualizarDashboardFinanceiro();
                    if (_modoAtual == "ATIVOS" || _modoAtual == "HOJE" || _modoAtual == "PENDENTES")
                        ExecutarBuscaReal();
                });
            }
            catch
            {
                // não falhar fluxo principal
            }
        }
        private void AddPendentePago(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return;
            try
            {
                var trimmed = nome.Trim();
                if (_listaPendentesPagos == null) _listaPendentesPagos = new List<string>();
                if (_listaPendentesPagos.Any(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase))) return;
                _listaPendentesPagos.Add(trimmed);
                SalvarPendentesPagosNoDispositivo();
            }
            catch
            {
                // não falhar fluxo principal
            }
        }
        private void SalvarPendentesPagosNoDispositivo()
        {
            try { Preferences.Default.Set(KEY_PENDENTES_PAGOS, JsonConvert.SerializeObject(_listaPendentesPagos)); }
            catch { /* não falhar a UI */ }
        }
        private void CarregarPendentesPagosDoDispositivo()
        {
            try
            {
                var json = Preferences.Default.Get(KEY_PENDENTES_PAGOS, "");
                if (!string.IsNullOrEmpty(json)) _listaPendentesPagos = JsonConvert.DeserializeObject<List<string>>(json) ?? new();
            }
            catch { _listaPendentesPagos = new List<string>(); }
        }       
        private void SalvarPendentesNoDispositivo() => Preferences.Default.Set("lista_pendentes_salva", JsonConvert.SerializeObject(_listaPendentesLocal));
        private void CarregarPendentesDoDispositivo()
        {
            var json = Preferences.Default.Get("lista_pendentes_salva", "");
            if (!string.IsNullOrEmpty(json)) _listaPendentesLocal = JsonConvert.DeserializeObject<List<ClientesHoje>>(json) ?? new();
        }
        private int GetRenovacoesCount(string cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente)) return 0;
            var k = $"renovacoes_{SanitizeKey(cliente)}";
            return Preferences.Default.Get(k, 0);
        }
        private void IncrementRenovacoesCount(string cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente)) return;
            var k = $"renovacoes_{SanitizeKey(cliente)}";
            int current = Preferences.Default.Get(k, 0);
            Preferences.Default.Set(k, current + 1);
        }
        private static string SanitizeKey(string s) =>
            string.IsNullOrWhiteSpace(s) ? "" : Regex.Replace(s, @"[^\w]", "_").ToLowerInvariant();

        #endregion

        #region Tela Gestão Financeira
        private async Task AbrirGestaoFinanceira()
        {
            // garante registro diário (se app foi aberto e ainda não registrou hoje)
            RegistrarCustoDiarioMeuAnuncioIfNeeded();
            CarregarInvestimentos();
            RegistrarCustoDiarioFundoIfNeeded();

            // calcula soma das NFs do mês atual (filtra por data válida) e atualiza faturamento exibido


            var background = Color.FromArgb("#F5F7FB");
            var scroll = new ScrollView { BackgroundColor = background };
            var layout = new VerticalStackLayout { Padding = 12, Spacing = 8, BackgroundColor = background };

            // lê custo por dia e acumulado mensal de anúncios
            double custoPorDia = Preferences.Default.Get(KEY_CUSTO_POR_DIA, 8.0);
            double acumuladoAnunciosMes = Preferences.Default.Get(KEY_CUSTO_ANUNCIOS_MES, 0.0);

            // calcula valores base
            // calcula valores base
            double somaTotalPlanos = 0;
            var listaAtivosOk = _listaCompletaServidor.Where(c => c.Ativo?.Trim().ToLower() == "ok").ToList();
            foreach (var cli in listaAtivosOk)
            {
                somaTotalPlanos += ObterValorPorNomePlano(cli.Plano);
            }
            // Agora calculamos os dias dos planos com base nas NFs do mês (lista que vai sendo incrementada)
            // ---------------------------------------
            var mesAtual = DateTime.Now.Month;
            var anoAtual = DateTime.Now.Year;

            // soma dos dias vindos das NFs do mês corrente
            double totalDiasNfMes = (_listaNfLocal ?? Enumerable.Empty<NfModel>())
                .Select(n => new { Nf = n, Dt = ParseNfDate(n.Data) })
                .Where(x => x.Dt != null && x.Dt.Value.Month == mesAtual && x.Dt.Value.Year == anoAtual)
                .Sum(x => x.Nf.Dias > 0 ? x.Nf.Dias : ObterDiasPorPlano(x.Nf.Plano));

            // custo de anúncios derivado das NFs do mês atual (dias * custoPorDia)
            double custoAnunciosClientesPorDias = Math.Round(totalDiasNfMes * custoPorDia, 2);

            _impostoBaseAtivaTotal = somaTotalPlanos * 0.06;
            _saldoContaInformado = Preferences.Default.Get("saldo_dia", 0.0);
            _gastoCartaoInformado = Preferences.Default.Get("cartao_dia", 0.0);

            // custo do meu anúncio para projeção diária (apenas leitura aqui)
            _meuAnuncioAtivo = Preferences.Default.Get("meu_anuncio_ativo", _meuAnuncioAtivo);
            double custoMeuAnuncioHoje = _meuAnuncioAtivo ? 100.00 : 0.00;

            // ---------------------------------------
            // Cabeçalho e ações (ordem mantida)
            // ---------------------------------------
            layout.Add(new Label { Text = "GESTÃO FINANCEIRA", FontSize = 20, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#263238") });

            var gridAcoes = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 10 };
            gridAcoes.Add(CriarBotaoAcao("📢 RELATÓRIO", "#1976D2", async () => await EnviarRelatorioWhatsapp()), 0, 0);
            gridAcoes.Add(CriarBotaoAcao("📂 HISTÓRICO", "#455A64", async () => await VerHistoricoMensal()), 1, 0);
            layout.Add(gridAcoes);

            // Saldo e Gastos (mantidos) — menor padding interno
            layout.Add(CriarCardEditavel("🏦", "SALDO ATUAL EM CONTA", _saldoContaInformado, Color.FromArgb("#2E7D32"), async () => { await EditarSaldos(); await AbrirGestaoFinanceira(); }));
            layout.Add(CriarCardNaoEditavel("💳", "GASTOS CARTÃO (MÊS)", _gastoCartaoInformado, Color.FromArgb("#C62828")));
            // novo card editável para custo por dia
            layout.Add(CriarCardEditavel("📊", "CUSTO POR DIA (ANÚNCIO)", custoPorDia, Colors.OrangeRed, async () =>
            {
                await EditarValorFinanceiro(KEY_CUSTO_POR_DIA, "Custo por dia (R$)");
                await AbrirGestaoFinanceira();
            }));

            // ---------------------------------------
            // RESUMO DO MÊS (centralizado e compacto)
            // ---------------------------------------
            var subt = new Label
            {
                Text = "RESUMO DO MÊS",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            layout.Add(subt);





            double faturamentoTotal = GetTotalNfMesPersistido();
            double impostosTotal = Math.Round(faturamentoTotal * 0.06, 2);
            double meuAnuncioTotalMes = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);

            // RegistrarCustoDiarioMeuAnuncioIfNeeded() é chamado no início de AbrirGestaoFinanceira,
            // portanto a preferência já deve conter o acumulado correto do mês.
            // Não ajustar/manualizar o valor aqui (evita duplicação).

            // inclui acumulado mensal de anúncios (renovações contabilizadas dia-a-dia)
            double custoAnunciosTotal = acumuladoAnunciosMes;

            // agora calcula lucro projetado subtraindo explicitamente o custoAnunciosTotal e os gastos do cartão
            double gastoCartaoMes = Preferences.Default.Get("cartao_dia", 0.0);
            double lucroProjetadoMes = Math.Round(faturamentoTotal - impostosTotal - custoAnunciosTotal - meuAnuncioTotalMes, 2);

            // (opcional) log temporário para depuração — remova depois
            System.Diagnostics.Debug.WriteLine($"DEBUG Lucro: faturamento={faturamentoTotal}, imposto={impostosTotal}, anunciosClientes={custoAnunciosClientesPorDias}, acumuladoAnunciosMes={acumuladoAnunciosMes}, meuAnuncio={meuAnuncioTotalMes}, custoAnunciosTotal={custoAnunciosTotal}, cartao={gastoCartaoMes}, lucroProjetado={lucroProjetadoMes}");

            // Receita disponível = saldo atual em conta - gasto cartão (mês)
            double receitaDisponivel = Math.Round(_saldoContaInformado - _gastoCartaoInformado, 2);

            Color corLucroMes = lucroProjetadoMes >= 0 ? Color.FromArgb("#1B5E20") : Color.FromArgb("#B71C1C");

            var lucroMesCard = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                Background = Color.FromArgb("#FFFDF0"),
                Stroke = Color.FromArgb("#FFFDF0"),
                Padding = 8,
                Margin = new Thickness(0, 0, 0, 2), // bem compacto em relação ao próximo bloco
                Content = new VerticalStackLayout
                {
                    Spacing = 1,
                    Children =
                    {
                        new Label { Text = "LUCRO PROJETADO (MÊS)", FontSize = 13, TextColor = corLucroMes, HorizontalTextAlignment = TextAlignment.Center, FontAttributes = FontAttributes.Bold }, // antes 12
                        new Label { Text = $"{lucroProjetadoMes:C2}", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = corLucroMes, HorizontalTextAlignment = TextAlignment.Center } // antes 20
                    }
                }
            };
            layout.Add(lucroMesCard);

            // ---------------------------------------
            // Frame com o restante do resumo — spacing reduzido
            // ---------------------------------------
            var frameRel = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 10,
                Content = new VerticalStackLayout { Spacing = 4 } // bem compacto internamente
            };
            var stackRel = (VerticalStackLayout)frameRel.Content;

            // Faturamento — verde mais escuro e fonte maior
            stackRel.Add(new Label { Text = $"💰 Faturamento: {faturamentoTotal:C2}", FontSize = 14, TextColor = Color.FromArgb("#1B5E20"), Margin = new Thickness(0, 2, 0, 0) }); // antes 13 e #00C853

            // Imposto, Anúncios e Meu Anúncio — aumentar levemente as fontes
            stackRel.Add(new Label { Text = $"💸 Imposto (6%): {impostosTotal:C2}", FontSize = 14, TextColor = Colors.Red, Margin = new Thickness(0, 2, 0, 0) });
            stackRel.Add(new Label { Text = $"📢 Anúncios: {custoAnunciosTotal:C2}", FontSize = 14, TextColor = Colors.Red, Margin = new Thickness(0, 2, 0, 0) });
            stackRel.Add(new Label { Text = $"📣 Meu Anúncio (mês acumulado): {meuAnuncioTotalMes:C2}", FontSize = 14, TextColor = Colors.Red, Margin = new Thickness(0, 2, 0, 0) });

            // separador visual com margem mínima
            stackRel.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#F0F3F8"), HorizontalOptions = LayoutOptions.Fill, Margin = new Thickness(0, 4, 0, 2) });

            // ---------------------------------------
            // Receita Disponível — aumentar fonte
            // ---------------------------------------
            Color corReceita = receitaDisponivel >= 0 ? Color.FromArgb("#1B5E20") : Color.FromArgb("#B71C1C");

            var receitaCard = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(12) },
                Background = receitaDisponivel >= 0 ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE"),
                Stroke = receitaDisponivel >= 0 ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE"),
                Padding = 10,
                Margin = new Thickness(0, 2, 0, 0), // reduzido: colar com conteúdo acima
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = "RECEITA DISPONÍVEL", FontSize = 12, TextColor = corReceita, HorizontalTextAlignment = TextAlignment.Center }, // antes 11
                        new Label { Text = $"{receitaDisponivel:C2}", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = corReceita, HorizontalTextAlignment = TextAlignment.Center } // antes 22
                    }
                }
            };

            // adiciona o frame de resumo e o card de receita disponível
            layout.Add(frameRel);
            layout.Add(receitaCard);

            // ---------------------------------------
            // Meu anúncio: switch — aproximado do card acima (margem mínima)
            // ---------------------------------------
            var switchMeuAnuncio = new Switch { IsToggled = _meuAnuncioAtivo, HorizontalOptions = LayoutOptions.End, ThumbColor = Color.FromArgb("#1976D2") };
            var lblToggle = new Label { Text = _meuAnuncioAtivo ? "ATIVO" : "INATIVO", VerticalOptions = LayoutOptions.Center, FontSize = ObterFonteResponsiva("ATIVO", 12, 10) };

            switchMeuAnuncio.Toggled += (s, e) =>
            {
                _meuAnuncioAtivo = e.Value;
                Preferences.Default.Set("meu_anuncio_ativo", _meuAnuncioAtivo);
                lblToggle.Text = _meuAnuncioAtivo ? "ATIVO" : "INATIVO";
                AtualizarDashboardFinanceiro();
            };

            var hSwitch = new HorizontalStackLayout
            {
                Spacing = 6,
                Margin = new Thickness(0, 2, 0, 0),
                Children =
                {
                    new Label { Text = "MEU ANÚNCIO", FontSize = ObterFonteResponsiva("MEU ANÚNCIO", 13, 10), VerticalOptions = LayoutOptions.Center, TextColor = Colors.Gray }, // antes sem TextColor (assumiu preto)
                    lblToggle,
                    switchMeuAnuncio
                }
            };
            layout.Add(hSwitch);

            // botão de acesso à Finança Pessoal
            layout.Add(CriarBotaoEstilizado("FINANÇA PESSOAL", Color.FromArgb("#6A1B9A"), async () => await AbrirFinancaPessoal(lucroProjetadoMes)));




            // voltar
            layout.Add(CriarBotaoEstilizado("← VOLTAR AO OPERACIONAL", Color.FromArgb("#9E9E9E"), AbrirTelaOperacional));

            scroll.Content = layout;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(scroll);
                _floatBtn.IsVisible = false;
                _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
            });
        }
        private async Task EditarSaldos()
        {
            double atualPessoal = Preferences.Default.Get("saldo_pessoal", 0.0);
            double atualCartaoPessoal = Preferences.Default.Get("cartao_pessoal", 0.0);
            double atualEmpresa = Preferences.Default.Get("saldo_empresa", 0.0);
            double atualCartaoEmpresa = Preferences.Default.Get("cartao_empresa", 0.0);

            string sugestaoP = atualPessoal.ToString("N2", new CultureInfo("pt-BR"));
            string inputP = await DisplayPromptAsync("Saldo Pessoal", "Insira o valor do Saldo Pessoal (ex: 1500,50):", "Salvar", "Cancelar", sugestaoP, -1, Keyboard.Numeric);
            if (inputP == null) return;

            string sugestaoCP = atualCartaoPessoal.ToString("N2", new CultureInfo("pt-BR"));
            string inputCP = await DisplayPromptAsync("Gastos Pessoais (Cartão)", "Insira o valor dos Gastos Pessoais (ex: 150,50):", "Salvar", "Cancelar", sugestaoCP, -1, Keyboard.Numeric);
            if (inputCP == null) return;

            string sugestaoE = atualEmpresa.ToString("N2", new CultureInfo("pt-BR"));
            string inputE = await DisplayPromptAsync("Saldo Empresa", "Insira o valor do Saldo Empresa (ex: 1500,50):", "Salvar", "Cancelar", sugestaoE, -1, Keyboard.Numeric);
            if (inputE == null) return;

            string sugestaoCE = atualCartaoEmpresa.ToString("N2", new CultureInfo("pt-BR"));
            string inputCE = await DisplayPromptAsync("Gastos Empresariais (Cartão)", "Insira o valor dos Gastos Empresariais (ex: 150,50):", "Salvar", "Cancelar", sugestaoCE, -1, Keyboard.Numeric);
            if (inputCE == null) return;

            double valP = ParseValor(inputP);
            double valE = ParseValor(inputE);
            double total = Math.Round(valP + valE, 2);

            double valCP = ParseValor(inputCP);
            double valCE = ParseValor(inputCE);
            double totalC = Math.Round(valCP + valCE, 2);

            Preferences.Default.Set("saldo_pessoal", valP);
            Preferences.Default.Set("saldo_empresa", valE);
            Preferences.Default.Set("saldo_dia", total);

            Preferences.Default.Set("cartao_pessoal", valCP);
            Preferences.Default.Set("cartao_empresa", valCE);
            Preferences.Default.Set("cartao_dia", totalC);

            // atualiza variáveis locais usadas na UI
            _saldoContaInformado = total;
            _gastoCartaoInformado = totalC;

            await DisplayAlert("Sucesso", $"Saldo atualizado: R$ {total:N2}\nGastos do cartão atualizados: R$ {totalC:N2}", "OK");
            AtualizarDashboardFinanceiro();
        }
        private async Task EditarValorFinanceiro(string chavePref, string nomeExibicao)
        {
            string resultado = await DisplayPromptAsync(
                "Editar " + nomeExibicao,
                "Insira o novo valor (Ex: 1500,50):",
                "Salvar",
                "Cancelar",
                "0,00",
                -1,
                Keyboard.Numeric);

            if (!string.IsNullOrEmpty(resultado))
            {
                if (double.TryParse(resultado.Replace(".", ","), out double novoValor))
                {
                    Preferences.Default.Set(chavePref, novoValor);
                }
                else
                {
                    await DisplayAlert("Erro", "Valor inválido! Use apenas números e vírgula.", "OK");
                }
            }
        }
        private async Task AddVariableExpensePrompt()
        {
            // escolha rápida de categoria
            string categoria = await DisplayActionSheet("Categoria do gasto", "Cancelar", null, "Mercado", "Cigarro", "Combustível", "Outro");
            if (string.IsNullOrEmpty(categoria) || categoria == "Cancelar") return;

            string desc;
            if (categoria == "Outro")
            {
                // descrição livre (comportamento anterior)
                desc = await DisplayPromptAsync("Gasto Variável", "Descrição do gasto:", "Adicionar", "Cancelar", placeholder: "", maxLength: 200, keyboard: Keyboard.Default);
                if (string.IsNullOrWhiteSpace(desc)) return;
            }
            else
            {
                // categoria escolhida -> usar como descrição
                desc = categoria;
            }

            string valorRaw = await DisplayPromptAsync("Gasto Variável", "Valor (ex: 150,00):", "Salvar", "Cancelar", "0,00", -1, Keyboard.Numeric);
            if (valorRaw == null) return;

            // normaliza e parseia
            double valor = 0;
            double.TryParse(valorRaw.Replace(".", ",").Replace("R$", ""), NumberStyles.Any, new CultureInfo("pt-BR"), out valor);
            if (valor <= 0)
            {
                // tenta fallback usando ParseValor se o usuário inseriu formato com R$
                valor = ParseValor(valorRaw);
            }

            // Sempre adicionar o gasto variável à lista (inclui Mercado/Cigarro/Combustível)
            var variable = new VariableExpense { Description = desc.Trim(), Value = Math.Round(valor, 2), Date = DateTime.Now.ToString("dd/MM/yyyy") };
            _variableExpenses.Add(variable);



            // se for uma das categorias específicas, acumula no fixed correspondente
            var categoriasAcumulaveis = new[] { "Mercado", "Cigarro", "Combustível" };
            if (categoriasAcumulaveis.Any(c => string.Equals(c, desc.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                AccumulateVariableInFixed(desc.Trim(), valor);
                var variableRed = new VariableExpense { Description = desc.Trim(), Value = Math.Round(valor, 2), Date = DateTime.Now.ToString("dd/MM/yyyy") };
                _variableExpensesReds.Add(variableRed);
            }

            SalvarDespesasFinancas();

            await DisplayAlert("Sucesso", "Gasto variável adicionado.", "OK");
        }
        private void AccumulateVariableInFixed(string category, double value)
        {
            if (string.IsNullOrWhiteSpace(category) || value <= 0) return;

            // procura fixed existente (case-insensitive)
            var existente = _fixedExpenses.FirstOrDefault(f => string.Equals(f.Name?.Trim(), category, StringComparison.OrdinalIgnoreCase));
            var categoriasAcumulaveis = new HashSet<string>(new[] { "Mercado", "Cigarro", "Combustível" }, StringComparer.OrdinalIgnoreCase);

            var categoriaTrim = category.Trim();
            if (!categoriasAcumulaveis.Contains(categoriaTrim)) return;

            if (existente == null)
            {
                // cria com vencimento dia 20 (conforme regra de visto) e Included = false (usuário marcará o visto)
                existente = new FixedExpense
                {
                    Name = category,
                    DayOfMonth = 20,
                    Value = Math.Round(value, 2),
                    Included = false
                };

                _fixedExpenses.Add(existente);


            }
            else
            {
                existente.Value = Math.Round(existente.Value + value, 2);

            }

            SalvarDespesasFinancas();
        }
        private async Task AddFixedExpensePrompt()
        {
            string nome = await DisplayPromptAsync("Gasto Fixo", "Nome do gasto (ex: Aluguel):", "Adicionar", "Cancelar", placeholder: "");
            if (string.IsNullOrWhiteSpace(nome)) return;

            string diaRaw = await DisplayPromptAsync("Gasto Fixo", "Dia do mês em que começa a vigorar (1-31):", "Salvar", "Cancelar", "1", -1, Keyboard.Numeric);
            if (diaRaw == null) return;
            if (!int.TryParse(diaRaw, out int dia) || dia < 1 || dia > 31) { await DisplayAlert("Erro", "Dia inválido.", "OK"); return; }

            string valorRaw = await DisplayPromptAsync("Gasto Fixo", "Valor (ex: 150,00):", "Salvar", "Cancelar", "0,00", -1, Keyboard.Numeric);
            if (valorRaw == null) return;
            double valor = 0; double.TryParse(valorRaw.Replace(".", ",").Replace("R$", ""), out valor);

            var f = new FixedExpense { Name = nome.Trim(), DayOfMonth = dia, Value = valor, Included = DateTime.Now.Day >= dia };
            _fixedExpenses.Add(f);
            SalvarDespesasFinancas();

            await DisplayAlert("Sucesso", "Gasto fixo adicionado.", "OK");
        }
        void AtualizarDashboardFinanceiro(double custoAnunciosHoje = 0)
        {
            RegistrarCustoDiarioMeuAnuncioIfNeeded();

            var mesAtual = DateTime.Now.Month;
            var anoAtual = DateTime.Now.Year;

            double faturamentoTotal = GetTotalNfMesPersistido();
            double impostosTotal = Math.Round(faturamentoTotal * 0.06, 2);

            double custoPorDia = Preferences.Default.Get(KEY_CUSTO_POR_DIA, 8.0);

            // apenas lê o acumulado já salvo quando houve renovação
            double custoAnunciosRenovados = Preferences.Default.Get(KEY_CUSTO_ANUNCIOS_MES, 0.0);

            double meuAnuncioTotalMes = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);

            double custoAnunciosTotal = custoAnunciosRenovados;

            double liquidoMensal = Math.Round(faturamentoTotal - impostosTotal - custoAnunciosTotal, 2);

            // ALERTA DE DEBUG
            MainThread.BeginInvokeOnMainThread(async () =>
            {

                // atualizar UI
                labelFaturamento.Text = $"💰 Faturamento: {faturamentoTotal:C2}";
                labelFaturamento.TextColor = Color.FromArgb("#1B5E20");

                labelImposto.Text = $"💸 Imposto (6%): {impostosTotal:C2}";

                if (labelCustoAnuncio != null)
                    labelCustoAnuncio.Text = $"📢 Anúncios: {custoAnunciosTotal:C2}";

                labelSaldoLiquido.Text = $"📈 Lucro Real: {liquidoMensal:C2}";

                labelMeuAnuncio.Text = $"Meu Anúncio (mês acumulado): {meuAnuncioTotalMes:C2}";
                labelMeuAnuncio.TextColor = Colors.Gray;

                var rel = new RelatorioFinanceiro
                {
                    FaturamentoTotal = faturamentoTotal,
                    ImpostosTotal = impostosTotal,
                    CustoAnuncio = custoAnunciosTotal,
                    TotalRenovacoes = _listaRenovadosHoje?.Count ?? 0,
                    MeuAnuncioTotal = meuAnuncioTotalMes
                };

                Preferences.Default.Set("relatorio_mensal", JsonConvert.SerializeObject(rel));
            });
        }
        private void CarregarDespesasFinancas()
        {
            try
            {
                var jsonF = Preferences.Default.Get(KEY_FIXED, "");
                if (!string.IsNullOrEmpty(jsonF))
                    _fixedExpenses = JsonConvert.DeserializeObject<List<FixedExpense>>(jsonF) ?? new List<FixedExpense>();

                var jsonV = Preferences.Default.Get(KEY_VAR, "");
                if (!string.IsNullOrEmpty(jsonV))
                    _variableExpenses = JsonConvert.DeserializeObject<List<VariableExpense>>(jsonV) ?? new List<VariableExpense>();

                var jsonVAR = Preferences.Default.Get(KEY_VARRED, "");
                if (!string.IsNullOrEmpty(jsonVAR))
                    _variableExpensesReds = JsonConvert.DeserializeObject<List<VariableExpense>>(jsonVAR) ?? new List<VariableExpense>();
            }
            catch
            {
                _fixedExpenses = new List<FixedExpense>();
                _variableExpenses = new List<VariableExpense>();
                _variableExpensesReds = new List<VariableExpense>();
            }

            // garante que o imposto do mês anterior exista como gasto fixo (vencimento dia 20)
            EnsureImpostoAnteriorExists();

            // garante valores padrões para custo por dia e acumulado mensal (se não existirem)
            if (!Preferences.Default.ContainsKey(KEY_CUSTO_POR_DIA))
                Preferences.Default.Set(KEY_CUSTO_POR_DIA, 8.0); // valor default anterior era 11

            if (!Preferences.Default.ContainsKey(KEY_CUSTO_ANUNCIOS_MES))
                Preferences.Default.Set(KEY_CUSTO_ANUNCIOS_MES, 0.0);
        }
        private void SalvarDespesasFinancas()
        {
            try
            {
                Preferences.Default.Set(KEY_FIXED, JsonConvert.SerializeObject(_fixedExpenses));
                Preferences.Default.Set(KEY_VAR, JsonConvert.SerializeObject(_variableExpenses));
                Preferences.Default.Set(KEY_VARRED, JsonConvert.SerializeObject(_variableExpensesReds));
            }
            catch
            {
                // persistência falhou — não quebrar app
            }
        }
        private void RegistrarCustoDiarioMeuAnuncioIfNeeded()
        {
            try
            {
                string ultimo = Preferences.Default.Get("meu_anuncio_ultimo_dia", "");
                string hoje = DateTime.Now.ToString("yyyy-MM-dd");
                if (ultimo != hoje)
                {
                    // se ativo hoje, acumula R$100 no total do mês
                    bool ativo = Preferences.Default.Get("meu_anuncio_ativo", _meuAnuncioAtivo);
                    if (ativo)
                    {
                        double total = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);
                        total += 100.0;
                        Preferences.Default.Set("meu_anuncio_total_mes", total);
                    }
                    Preferences.Default.Set("meu_anuncio_ultimo_dia", hoje);
                }
            }
            catch
            {
                // não falhar se Preferences der problema
            }
        }

        #endregion

        #region Tela Finança Pessoal
        private async Task AbrirFinancaPessoal(double lucroProjetadoMes)
        {
            CarregarDespesasFinancas();
            CarregarInvestimentos();

            var background = Color.FromArgb("#FFFFFF");
            var layout = new VerticalStackLayout { Padding = 14, Spacing = 10, BackgroundColor = background };



            layout.Add(new Label { Text = "FINANÇA PESSOAL", FontSize = 20, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, TextColor = Colors.Black });




            // Totais e cálculo de despesas
            int hojeDia = DateTime.Now.Day;
            // soma apenas os fixos que estão com checkbox marcado, independente do dia
            // soma apenas os fixos que estão com checkbox marcado
            double totalFixedIncluded = _fixedExpenses
                .Where(f => f.Included)
                .Sum(f => f.Value);

            // soma apenas os fixos que NÃO estão marcados
            double totalFixedNotIncluded = _fixedExpenses
                .Where(f => !f.Included)
                .Sum(f => f.Value);



            double totalVariable = _variableExpenses.Sum(v => v.Value);

            double totalVariableRed = _variableExpensesReds.Sum(v => v.Value);

            double meuAnuncioTotalMes = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);

            double saldoInvestimento = _fundoSaldo;

            // ---captura explicitamente valores de imposto já cadastrados em fixed expenses ---
            double impostoMesAnterior = Preferences.Default.Get("imposto_mes_anterior", 0.0);





            // custo de anúncios baseado nos dias restantes dos planos ativos (NOVO: somar ao gastos do mês)
            double custoPorDia = Preferences.Default.Get(KEY_CUSTO_POR_DIA, 8.0);
            double custoAnunciosDiasRestantes = 0.0;

            double fixoCalculado = totalVariable - totalVariableRed;
            try
            {
                var ativosOk = _listaCompletaServidor.Where(c => c.Ativo?.Trim().ToLower() == "ok");
                foreach (var cli in ativosOk)
                {
                    var dtFim = ParseNfDate(cli.Fim);
                    if (dtFim != null)
                    {
                        int diasRestantes = (dtFim.Value.Date - DateTime.Now.Date).Days;
                        if (diasRestantes > 0)
                            custoAnunciosDiasRestantes += diasRestantes * custoPorDia;
                    }
                }
                custoAnunciosDiasRestantes = Math.Round(custoAnunciosDiasRestantes, 2);
            }
            catch
            {
                custoAnunciosDiasRestantes = 0.0;
            }

            double gastosMes = totalFixedIncluded + fixoCalculado + impostoMesAnterior + custoAnunciosDiasRestantes + saldoInvestimento;



            // Receita base (mesma lógica usada na tela Gestão Financeira)
            double receitaBase = Preferences.Default.Get("saldo_dia", 0.0) - Preferences.Default.Get("cartao_dia", 0.0);

            // Receita disponível após subtrair todos os custos fixos aplicáveis hoje e variáveis
            double receitaDepoisDespesas = Math.Round(receitaBase - custoAnunciosDiasRestantes - totalFixedNotIncluded - saldoInvestimento, 2);

            // Resumo (totais)
            var resumoGrid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
                RowDefinitions =
                {
                    new RowDefinition(), // Lucro projetado
                    new RowDefinition(), // Fixos aplicáveis
                    new RowDefinition(), // Variáveis
                    new RowDefinition(), // Gastos mês
                    new RowDefinition(),  // Receita disponível
                    new RowDefinition(),  // Receita disponível
                    new RowDefinition()  // Receita disponível
                },
                RowSpacing = 6
            };
            // Receita em verde com emoji de dinheiro
            resumoGrid.Add(CriarCardNaoEditavel("💰", "RECEITA", receitaBase, Color.FromArgb("#1B5E20")), 0, 1);

            // Gastos do mês em vermelho com emoji de carrinho
            resumoGrid.Add(CriarCardNaoEditavel("🛒", "GASTO (MÊS)", gastosMes, Color.FromArgb("#C62828")), 0, 2);


            resumoGrid.Add(new Label { Text = $"Fixos pagos: {totalFixedIncluded:C2}", TextColor = Colors.Blue }, 0, 3);
            resumoGrid.Add(new Label { Text = $"Fixos pendentes: {totalFixedNotIncluded:C2}", TextColor = Colors.Red }, 0, 4);
            resumoGrid.Add(new Label { Text = $"Variáveis (total): {totalVariable:C2}", TextColor = Colors.Red }, 0, 5);
            resumoGrid.Add(new Label { Text = $"Custo anúncios (Dias restantes): {custoAnunciosDiasRestantes:C2}", TextColor = Colors.Red }, 0, 6);
            resumoGrid.Add(new Label { Text = $"Saldo Investimento: {saldoInvestimento:C2}", TextColor = Color.FromArgb("#1B5E20") }, 0, 7);






            layout.Add(resumoGrid);

            // Lista fixa visível (sem rolagem)
            layout.Add(new Label { Text = "Gastos Fixos", FontAttributes = FontAttributes.Bold, TextColor = Colors.Gray, FontSize = ObterFonteResponsiva("Gastos Fixos", 16, 12) });
            var stackFixed = new VerticalStackLayout { Spacing = 6 };
            foreach (var f in _fixedExpenses.OrderBy(x => x.DayOfMonth))
            {
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8, Padding = new Thickness(6) };
                var txtDay = new Label { Text = $"dia {f.DayOfMonth}", FontSize = 12, VerticalOptions = LayoutOptions.Center, TextColor = Colors.DimGray };
                var txtName = new Label { Text = $"{f.Name}", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, TextColor = Colors.Black };
                var txtValue = new Label { Text = $"{f.Value:C2}", VerticalOptions = LayoutOptions.Center, TextColor = Colors.Red };

                var chk = new CheckBox { IsChecked = f.Included, Color = Colors.RoyalBlue, VerticalOptions = LayoutOptions.Center };
                chk.CheckedChanged += (s, e) =>
                {
                    f.Included = e.Value;
                    SalvarDespesasFinancas();
                    MainThread.BeginInvokeOnMainThread(async () => await AbrirFinancaPessoal(lucroProjetadoMes));
                };

                row.Add(txtDay, 0, 0);
                row.Add(new VerticalStackLayout { Children = { txtName }, Spacing = 2 }, 1, 0);
                row.Add(new HorizontalStackLayout { Spacing = 6, Children = { txtValue, chk } }, 2, 0);

                stackFixed.Add(new Border { Stroke = Color.FromArgb("#ECEFF4"), StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) }, Padding = 4, Content = row });
            }
            layout.Add(stackFixed);

            // Destaque: Receita disponível (após despesas) logo abaixo da lista de fixos, igual ao estilo da Gestão Financeira
            var destaqueBg = receitaDepoisDespesas >= 0 ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE");
            var destaqueTxt = receitaDepoisDespesas >= 0 ? Color.FromArgb("#1B5E20") : Color.FromArgb("#B71C1C");

            var receitaDestaque = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(12) },
                Background = destaqueBg,
                Stroke = destaqueBg,
                Padding = 12,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = "RECEITA DISPONÍVEL (APÓS DESPESAS)", FontSize = 12, TextColor = destaqueTxt, HorizontalTextAlignment = TextAlignment.Center, FontAttributes = FontAttributes.Bold },
                        new Label { Text = $"{receitaDepoisDespesas:C2}", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = destaqueTxt, HorizontalTextAlignment = TextAlignment.Center }
                    }
                }
            };
            layout.Add(receitaDestaque);

            // Botões de ação
            var gridAcoes = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }, ColumnSpacing = 8 };

            gridAcoes.Add(CriarBotaoEstilizado("+FIXO", Color.FromArgb("#1976D2"), async () => { string escolha = await DisplayActionSheet("Gerenciar Custos Fixos", "Cancelar", null, "Adicionar", "Remover"); if (escolha == "Adicionar") { await AddFixedExpensePrompt(); } else if (escolha == "Remover") { var nomesCustos = _fixedExpenses.Select(f => f.Name).ToArray(); if (nomesCustos.Length == 0) { await DisplayAlert("Aviso", "Não há custos fixos para remover.", "OK"); return; } string remover = await DisplayActionSheet("Escolha o custo fixo para remover", "Cancelar", null, nomesCustos); if (!string.IsNullOrEmpty(remover) && remover != "Cancelar") { var item = _fixedExpenses.FirstOrDefault(f => f.Name == remover); if (item != null) { _fixedExpenses.Remove(item); SalvarDespesasFinancas(); } } } await AbrirFinancaPessoal(lucroProjetadoMes); }), 0);
            gridAcoes.Add(CriarBotaoEstilizado("INCLUIR VARIÁVEL", Color.FromArgb("#2E7D32"), async () => { await AddVariableExpensePrompt(); await AbrirFinancaPessoal(lucroProjetadoMes); }), 1);

            layout.Add(gridAcoes);

            // Botão para ver / compartilhar variáveis
            layout.Add(CriarBotaoEstilizado("VER VARIÁVEIS / COMPARTILHAR", Color.FromArgb("#455A64"), async () =>
            {
                CarregarDespesasFinancas();
                if (_variableExpenses.Count == 0) { await DisplayAlert("Variáveis", "Nenhum gasto variável registrado.", "OK"); return; }
                var texto = new StringBuilder();
                foreach (var v in _variableExpenses)
                {
                    texto.AppendLine($"• {v.Date} — {v.Description} — {v.Value:C2}");
                }
                string escolha = await DisplayActionSheet("Ações", "Voltar", null, "Visualizar", "Compartilhar via WhatsApp", "Limpar variáveis");
                if (escolha == "Visualizar") await DisplayAlert("Gastos Variáveis", texto.ToString(), "OK");
                else if (escolha == "Compartilhar via WhatsApp")
                {
                    try { await Launcher.Default.OpenAsync($"https://wa.me/?text={Uri.EscapeDataString(texto.ToString())}"); }
                    catch { await Share.Default.RequestAsync(new ShareTextRequest { Title = "Gastos Variáveis", Text = texto.ToString() }); }
                }
                else if (escolha == "Limpar variáveis")
                {
                    bool conf = await DisplayAlert("Confirma", "Remover todos os gastos variáveis?", "Sim", "Não");
                    if (conf) { _variableExpenses.Clear(); _variableExpensesReds.Clear(); SalvarDespesasFinancas(); await AbrirFinancaPessoal(lucroProjetadoMes); }
                }
            }));

            layout.Add(CriarBotaoEstilizado("INVESTIMENTOS", Color.FromArgb("#0D47A1"), async () => await AbrirInvestimentos())); // << adicionar esta linha

            // Botão de voltar
            layout.Add(CriarBotaoEstilizado("VOLTAR", Color.FromArgb("#9E9E9E"), async () => await AbrirGestaoFinanceira()));

            // Envolver em ScrollView para habilitar rolagem como nas outras telas
            var scroll = new ScrollView { BackgroundColor = background, Content = layout };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _containerConteudo.Children.Clear();
                _containerConteudo.Children.Add(scroll);
                _floatBtn.IsVisible = false;
                _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
            });
        }
        private double ParseValor(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0;
            var cleaned = Regex.Replace(valor, @"[^\d,\.]", "");
            if (cleaned.Contains(",")) cleaned = cleaned.Replace(".", "").Replace(",", ".");
            else cleaned = cleaned.Replace(",", ".");
            if (double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double res)) return res;
            return 0;
        }
        private string GerarRelatorioMensal()
        {
            // calcula receita bruta do mês atual
            double receitaBruta = _listaNfLocal.Sum(nf => ParseValor(nf.Valor));

            // calcula imposto e salva
            double impostos = Math.Round(receitaBruta * 0.06, 2);
            Preferences.Default.Set("imposto_mes_anterior", impostos);



            // gera relatório detalhado
            return GerarRelatorioMensal(DateTime.Now);
        }
        private string GerarRelatorioMensal(DateTime mesRef)
        {
            var ativosOk = _listaCompletaServidor.Where(c => c.Ativo?.Trim().ToLower() == "ok").ToList();
            double totalFaturamentoAtivos = 0;
            var resumo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var cli in ativosOk)
            {
                double valor = ObterValorPorNomePlano(cli.Plano);
                totalFaturamentoAtivos += valor;
                string nomeP = (cli.Plano ?? "OUTROS").ToUpper().Trim();
                if (resumo.ContainsKey(nomeP)) resumo[nomeP]++;
                else resumo[nomeP] = 1;
            }

            // soma NFs do mês
            double totalNfMes = 0;
            foreach (var nf in _listaNfLocal ?? Enumerable.Empty<NfModel>())
            {
                var dt = ParseNfDate(nf.Data);
                if (dt != null && dt.Value.Month == mesRef.Month && dt.Value.Year == mesRef.Year)
                    totalNfMes += ParseValor(nf.Valor);
            }

            double receitaBruta = totalFaturamentoAtivos + totalNfMes;
            double impostos = Math.Round(receitaBruta * 0.06, 2);
            double meuAnuncioTotal = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);

            // --- RENOVADOS DO MÊS ---
            double custoPorDia = Preferences.Default.Get(KEY_CUSTO_POR_DIA, 8.0);

            var renovadosMes = (_listaNfLocal ?? Enumerable.Empty<NfModel>())
                .Where(n =>
                {
                    var dt = ParseNfDate(n.Data);
                    return dt != null
                           && dt.Value.Month == mesRef.Month
                           && dt.Value.Year == mesRef.Year;

                })
                .ToList();

            int totalRenovadosMes = renovadosMes.Count;
            string nomesRenovados = totalRenovadosMes > 0
                ? string.Join("\n", renovadosMes.Select(r => $"✅ {r.Cliente} ({r.Plano}) - {r.Dias} dias"))
                : "Nenhum renovado.";

            // custo total de anúncios dos renovados (dias × custoPorDia)
            double custoAnunciosRenovadosMes = renovadosMes.Sum(r => (r.Dias > 0 ? r.Dias : ObterDiasPorPlano(r.Plano)) * custoPorDia);
            custoAnunciosRenovadosMes = Math.Round(custoAnunciosRenovadosMes, 2);

            // --- Montagem do relatório ---
            string texto = $"📊 *RELATÓRIO FINANCEIRO* - {mesRef:MM/yyyy}\n\n";
            texto += $"✅ *Total Ativos:* {ativosOk.Count}\n\n";
            foreach (var item in resumo) texto += $"• {item.Key}: {item.Value}\n";

            texto += $"\n💰 *Faturamento (base ativos):* R$ {totalFaturamentoAtivos:N2}";
            texto += $"\n🧾 *Entrada por NFs (mês):* R$ {totalNfMes:N2}";
            texto += $"\n💸 *Imposto (6% sobre receita bruta):* R$ {impostos:N2}";
            texto += $"\n📣 *Custo Meu Anúncio (mês):* R$ {meuAnuncioTotal:N2}";
            texto += $"\n📢 *Custo Anúncios Renovados (mês):* R$ {custoAnunciosRenovadosMes:N2}";

            texto += $"\n\n🔄 *Planos Renovados (Mês):* {totalRenovadosMes}";
            texto += $"\n{nomesRenovados}";

            texto += $"\n\n_Gerado automaticamente pelo App_";
            return texto;
        }
        private void AddToTotalNfMes(double amount)
        {
            if (amount <= 0) return;
            _totalNfMesPersistido = Math.Round(_totalNfMesPersistido + amount, 2);
            Preferences.Default.Set(KEY_TOTAL_NF_MES, _totalNfMesPersistido);
        }
        private double GetTotalNfMesPersistido() =>
            Preferences.Default.Get(KEY_TOTAL_NF_MES, _totalNfMesPersistido);

        #endregion

        #region Tela Investimentos
        private async Task AbrirInvestimentos()
        {
            await ExecutarComLoader(async () =>
            {
                CarregarInvestimentos();
                RegistrarCustoDiarioFundoIfNeeded();



                var background = Color.FromArgb("#FFFFFF");
                var layout = new VerticalStackLayout { Padding = 12, Spacing = 10, BackgroundColor = background };

                var valorInv = new Label { TextColor = Colors.Black };

                var preçoPorAtivo = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
                int updatedTodayCount = 0;
                int oldCacheCount = 0;
                int missingCount = 0;
                string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

                layout.Add(valorInv);

                var lblTitle = new Label
                {
                    Text = "INVESTIMENTOS",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = Colors.Black
                };
                layout.Add(lblTitle);

                layout.Add(CriarCardAdicionar("🏦", "SALDO INVESTIMENTO", _fundoSaldo,
                    Color.FromArgb("#2E7D32"), () => { _ = AdicionarAoFundoPrompt(); }));

                CarregarJurosCdbFromPrefs();
                await RegistrarJurosCdbIfNeeded();

                // Pre-fetch de preços
                foreach (var inv in _investments.OrderBy(i => i.Name))
                {
                    if (string.IsNullOrWhiteSpace(inv?.Name)) continue;
                    var invNameTrim = inv.Name.Trim();
                    if (invNameTrim.Equals("CDB PESSOAL", StringComparison.OrdinalIgnoreCase) ||
                        invNameTrim.Equals("CDB EMPRESA", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string symbol = invNameTrim + ".SA";
                    double? preco = await GetDailyStockPriceAsync(symbol);
                    preçoPorAtivo[inv.Name ?? ""] = preco;

                    var keyDate = $"stock_price_daily_{SanitizeKey(symbol.ToUpperInvariant())}_date";
                    var savedDate = Preferences.Default.Get(keyDate, "");
                    if (!string.IsNullOrEmpty(savedDate) && savedDate == today)
                        updatedTodayCount++;
                    else if (preco.HasValue)
                        oldCacheCount++;
                    else
                        missingCount++;
                }

                // Totais por categoria
                decimal totalRendaFixa = 0m;
                decimal totalAcoes = 0m;
                decimal totalFiis = 0m;

                foreach (var inv in _investments)
                {
                    double? preco = preçoPorAtivo.ContainsKey(inv.Name ?? "") ? preçoPorAtivo[inv.Name ?? ""] : null;
                    decimal unitPrice = (decimal)(preco ?? inv.PricePerUnit);
                    decimal valor = unitPrice * (decimal)inv.Quantity;

                    string invNameUpper = (inv.Name ?? "").Trim().ToUpperInvariant();

                    if (invNameUpper.Contains("CDB"))
                        totalRendaFixa += valor;
                    else if (invNameUpper.Contains("BBSE3") || invNameUpper.Contains("ITUB4") || invNameUpper.Contains("PETR4"))
                        totalAcoes += valor;
                    else
                        totalFiis += valor;
                }

                decimal somaTotal = totalRendaFixa + totalAcoes + totalFiis;
                somaTotal = Math.Round(somaTotal, 2);
                double jurosTotalCdb = Math.Round(jurosCdbPessoal + jurosCdbEmpresa, 2);
                double valorTotalComJuros = (double)somaTotal + jurosTotalCdb + _SaldoFake;

                // Card total
                layout.Add(CriarCardAdicionar("💼", "INVESTIMENTO TOTAL",
                    (double)valorTotalComJuros, Color.FromArgb("#1B5E20"), () => { _ = AdicionarAoFundoPromptFake(); }));

                // Porcentagens em cards modernos
                double pctRendaFixa = somaTotal > 0 ? (double)(totalRendaFixa / somaTotal * 100) : 0;
                double pctAcoes = somaTotal > 0 ? (double)(totalAcoes / somaTotal * 100) : 0;
                double pctFiis = somaTotal > 0 ? (double)(totalFiis / somaTotal * 100) : 0;

                layout.Add(CriarCardPercentual("📈", "Renda Fixa", pctRendaFixa, Color.FromArgb("#2E7D32")));
                layout.Add(CriarCardPercentual("💹", "Ações", pctAcoes, Color.FromArgb("#1565C0")));
                layout.Add(CriarCardPercentual("🏢", "FIIs", pctFiis, Color.FromArgb("#6A1B9A")));

                if (_investments.Count == 0)
                {
                    layout.Add(new Label
                    {
                        Text = "Nenhum investimento cadastrado.",
                        TextColor = Colors.DimGray,
                        HorizontalOptions = LayoutOptions.Center
                    });
                }
                else
                {
                    OrganizarInvestimentosPorCategoria(layout, _investments, preçoPorAtivo);
                }

                // Botões de ação
                var acoesStack = new VerticalStackLayout { Spacing = 8 };
                acoesStack.Add(CriarBotaoEstilizado("COMPRAR INVESTIMENTO", Color.FromArgb("#2E7D32"),
                    async () => { await ComprarInvestimentoPrompt(); await AbrirInvestimentos(); }));
                acoesStack.Add(CriarBotaoEstilizado("VOLTAR", Color.FromArgb("#9E9E9E"),
                    async () => await AbrirFinancaPessoal(0)));
                layout.Add(acoesStack);

                var scroll = new ScrollView { BackgroundColor = background, Content = layout };
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _containerConteudo.Children.Clear();
                    _containerConteudo.Children.Add(scroll);
                    _floatBtn.IsVisible = false;
                    _floatBtnG.Margin = new Thickness(0, 0, 14, 18);
                });

                // Notificação rápida
                string notice;
                if (updatedTodayCount == _investments.Count)
                    notice = "Preços atualizados hoje.";
                else if (updatedTodayCount > 0 && (oldCacheCount > 0 || missingCount > 0))
                    notice = "Alguns preços atualizados hoje; outros usam valores salvos anteriormente.";
                else if (oldCacheCount > 0)
                    notice = "Não foi possível buscar preços — usando valores salvos anteriormente.";
                else if (missingCount > 0)
                    notice = "Preços indisponíveis para alguns ativos.";
                else
                    notice = "Não foi possível obter preços.";

                _ = ShowTemporaryNotification(notice, 1300);
            });
        }
        private void CarregarInvestimentos()
        {
            try
            {
                _fundoSaldo = Preferences.Default.Get(KEY_FUNDO_SALDO, 0.0);
                var json = Preferences.Default.Get(KEY_INVESTMENTS, "");
                if (!string.IsNullOrEmpty(json))
                    _investments = JsonConvert.DeserializeObject<List<InvestmentCard>>(json) ?? new List<InvestmentCard>();
                else
                    _investments = new List<InvestmentCard>();
            }
            catch
            {
                _fundoSaldo = Preferences.Default.Get(KEY_FUNDO_SALDO, 0.0);
                _investments = new List<InvestmentCard>();
            }
        }
        private void SalvarInvestimentos()
        {
            try
            {
                Preferences.Default.Set(KEY_INVESTMENTS, JsonConvert.SerializeObject(_investments));
                Preferences.Default.Set(KEY_FUNDO_SALDO, _fundoSaldo);
            }
            catch
            {
                // não falhar a UI se persistência der problema
            }
        }
        private async Task AdicionarAoFundoPromptFake()
        {


            double valor = 0;
            double valorFake = 320000;


            if (fake) { _SaldoFake = Math.Round(valorFake, 2); fake = false; }
            else { _SaldoFake = Math.Round(valor, 2); fake = true; }
            //SalvarInvestimentos(); 

            await AbrirInvestimentos();
        }
        private async Task AdicionarAoFundoPrompt()
        {
            string input = await DisplayPromptAsync("Adicionar ao Fundo", "Valor a adicionar (ex: 100,00):", "Adicionar", "Cancelar", "0,00", -1, Keyboard.Numeric);
            if (input == null) return;

            double valor = ParseValor(input);
            if (valor <= 0)
            {
                await DisplayAlert("Erro", "Informe um valor válido.", "OK");
                return;
            }

            await ExecutarComLoader(async () =>
            {
                _fundoSaldo = Math.Round(_fundoSaldo + valor, 2);
                SalvarInvestimentos();
                await Task.Delay(150);
            });

            await DisplayAlert("Sucesso", $"Adicionados {valor:C2} ao fundo. Saldo atual: {_fundoSaldo:C2}", "OK");
            // Reabre a tela atual para refletir alterações (passa 0 — a tela recalcula internamente)
            await AbrirInvestimentos();
        }
        private async Task ComprarInvestimentoPrompt()
        {
            // oferta de sugestões a partir dos ativos já registrados
            var sugestoes = _investments
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => i.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

            string nome = null!;
            if (sugestoes.Count > 0)
            {
                var items = sugestoes.Concat(new[] { "Outro" }).ToArray();
                string escolha = await DisplayActionSheet("Escolha ativo ou 'Outro'", "Cancelar", null, items);
                if (string.IsNullOrEmpty(escolha) || escolha == "Cancelar") return;

                if (escolha == "Outro")
                {
                    nome = await DisplayPromptAsync("Comprar Investimento", "Nome do investimento:", "Próximo", "Cancelar", placeholder: "");
                    if (string.IsNullOrWhiteSpace(nome)) return;
                }
                else
                {
                    nome = escolha;
                }
            }
            else
            {
                nome = await DisplayPromptAsync("Comprar Investimento", "Nome do investimento:", "Próximo", "Cancelar", placeholder: "");
                if (string.IsNullOrWhiteSpace(nome)) return;
            }

            string qtdRaw = await DisplayPromptAsync("Quantidade de cotas", "Quantidade (ex: 1,5):", "Próximo", "Cancelar", "1,00", -1, Keyboard.Numeric);
            if (qtdRaw == null) return;
            double qtd = ParseValor(qtdRaw);
            if (qtd <= 0) { await DisplayAlert("Erro", "Quantidade inválida.", "OK"); return; }

            string precoRaw = await DisplayPromptAsync("Valor por cota", "Valor por cota (ex: 100,00):", "Comprar", "Cancelar", "0,00", -1, Keyboard.Numeric);
            if (precoRaw == null) return;
            double preco = ParseValor(precoRaw);
            if (preco <= 0) { await DisplayAlert("Erro", "Preço inválido.", "OK"); return; }

            double totalCusto = Math.Round(qtd * preco, 2);
            if (_fundoSaldo < totalCusto)
            {
                await DisplayAlert("Saldo insuficiente", $"Saldo do fundo: {_fundoSaldo:C2}\nCusto necessário: {totalCusto:C2}", "OK");
                return;
            }

            await ExecutarComLoader(async () =>
            {
                // debita do fundo
                _fundoSaldo -= totalCusto;

                // adiciona ou atualiza investimento com média ponderada do preço por cota
                var existe = _investments.FirstOrDefault(i => string.Equals(i.Name?.Trim(), nome.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existe == null)
                {
                    var novo = new InvestmentCard
                    {
                        Name = nome.Trim(),
                        Quantity = qtd,
                        PricePerUnit = preco
                    };
                    novo.History.Add(new PurchaseHistory { Date = DateTime.Now.ToString("dd/MM/yyyy"), Quantity = qtd, TotalInvested = totalCusto });
                    _investments.Add(novo);
                }
                else
                {
                    double quantidadeAntiga = existe.Quantity;
                    double investidoAntigo = quantidadeAntiga * existe.PricePerUnit;
                    double investidoNovo = qtd * preco;
                    double novaQuantidade = quantidadeAntiga + qtd;
                    double novoPrecoMedio = novaQuantidade > 0 ? (investidoAntigo + investidoNovo) / novaQuantidade : preco;

                    existe.Quantity = novaQuantidade;
                    existe.PricePerUnit = Math.Round(novoPrecoMedio, 2);
                    existe.History.Add(new PurchaseHistory { Date = DateTime.Now.ToString("dd/MM/yyyy"), Quantity = qtd, TotalInvested = totalCusto });
                }

                SalvarInvestimentos();
                await Task.Delay(200); // pequena pausa para UX
            });

            await DisplayAlert("Sucesso", $"Compra realizada: {FormatQuantity(qtd)} cotas por {preco:C2} (Total {totalCusto:C2})", "OK");
        }
        private async Task VenderInvestimentoPrompt(InvestmentCard inv)
        {
            if (inv == null) return;
            if (inv.Quantity <= 0) { await DisplayAlert("Erro", "Quantidade do ativo é zero.", "OK"); return; }

            // 1) solicitar quantidade a vender
            string sugestaoQtd = inv.Quantity.ToString("N2", new CultureInfo("pt-BR"));
            string qtdRaw = await DisplayPromptAsync("Vender Cotas", $"Quantidade a vender (max {inv.Quantity:N2}):", "Próximo", "Cancelar", sugestaoQtd, -1, Keyboard.Numeric);
            if (qtdRaw == null) return;
            double qtd = ParseValor(qtdRaw);
            if (qtd <= 0 || qtd > inv.Quantity) { await DisplayAlert("Erro", "Quantidade inválida.", "OK"); return; }

            // 2) solicitar valor por cota
            string sugestaoPreco = inv.PricePerUnit.ToString("N2", new CultureInfo("pt-BR"));
            string precoRaw = await DisplayPromptAsync("Valor por cota", "Informe o valor por cota recebido (ex: 100,00):", "Confirmar", "Cancelar", sugestaoPreco, -1, Keyboard.Numeric);
            if (precoRaw == null) return;
            double precoPorCota = ParseValor(precoRaw);
            if (precoPorCota <= 0) { await DisplayAlert("Erro", "Valor por cota inválido.", "OK"); return; }

            // 3) calcula total a devolver ao fundo
            double valorDevolvido = Math.Round(qtd * precoPorCota, 2);

            await ExecutarComLoader(async () =>
            {
                // reduz quantidade e credita o valor calculado ao fundo
                inv.Quantity = Math.Round(inv.Quantity - qtd, 8);
                _fundoSaldo = Math.Round(_fundoSaldo + valorDevolvido, 2);

                // se zerou, remove card
                if (inv.Quantity <= 0)
                {
                    _investments.RemoveAll(i => string.Equals(i.Name, inv.Name, StringComparison.OrdinalIgnoreCase));
                }

                SalvarInvestimentos();
                await Task.Delay(150);
            });

            await DisplayAlert("Venda realizada", $"Vendidas {FormatQuantity(qtd)} cotas — {precoPorCota:C2} por cota (Total {valorDevolvido:C2})\nValor devolvido ao fundo.", "OK");
        }
        private async Task MostrarHistoricoInvestimento(InvestmentCard inv)
        {
            if (inv == null) return;
            if (inv.History == null || inv.History.Count == 0) { await DisplayAlert("Histórico", "Nenhuma compra registrada para este investimento.", "OK"); return; }

            var sb = new StringBuilder();
            foreach (var h in inv.History.OrderByDescending(x => ParseNfDate(x.Date) ?? DateTime.Now))
            {
                string qtdTexto = FormatQuantity(h.Quantity); // usa FormatQuantity para evitar ",00" quando inteiro
                sb.AppendLine($"{h.Date} — {qtdTexto} cotas — {h.TotalInvested:C2}");
            }
            await DisplayAlert($"Histórico: {inv.Name}", sb.ToString(), "OK");
        }
        private string FormatQuantity(double q)
        {
            // se inteiro, exibe sem decimais; senão exibe com 2 casas (pt-BR)
            if (Math.Abs(q - Math.Round(q)) < 0.0001)
                return q.ToString("N0", new CultureInfo("pt-BR"));
            return q.ToString("N2", new CultureInfo("pt-BR"));
        }
        private void CarregarJurosCdbFromPrefs()
        {
            try
            {
                jurosCdbPessoal = Preferences.Default.Get(KEY_JUROS_CDB_PESSOAL, 0.0);
                jurosCdbEmpresa = Preferences.Default.Get(KEY_JUROS_CDB_EMPRESA, 0.0);
            }
            catch
            {
                jurosCdbPessoal = 0.0;
                jurosCdbEmpresa = 0.0;
            }
        }
        private void SaveJurosCdbToPrefs()
        {
            try
            {
                Preferences.Default.Set(KEY_JUROS_CDB_PESSOAL, Math.Round(jurosCdbPessoal, 2));
                Preferences.Default.Set(KEY_JUROS_CDB_EMPRESA, Math.Round(jurosCdbEmpresa, 2));
            }
            catch
            {
                // não falhar a UI
            }
        }
        private async Task RegistrarJurosCdbIfNeeded()
        {
            try
            {
                string ultimo = Preferences.Default.Get(KEY_JUROS_CDB_ULTIMO_DIA, "");
                string hoje = DateTime.Now.ToString("yyyy-MM-dd");

                // se já registrou hoje, não faz nada
                if (ultimo == hoje)
                    return;

                // taxa diária (exemplo: 10% ao ano ≈ 0,000261 ao dia)
                const double taxaDiaria = TAXA_DIARIA_CDB;

                foreach (var inv in _investments ?? Enumerable.Empty<InvestmentCard>())
                {
                    if (string.IsNullOrWhiteSpace(inv?.Name)) continue;
                    string n = inv.Name.Trim().ToUpperInvariant();
                    double baseValor = inv.Quantity * inv.PricePerUnit;

                    if (n.Contains("CDB") && n.Contains("PESSOAL"))
                    {
                        // se ainda não tem juros, já começa com lucro no primeiro dia
                        if (jurosCdbPessoal == 0)
                            jurosCdbPessoal = baseValor * taxaDiaria;
                        else
                            jurosCdbPessoal += baseValor * taxaDiaria;
                    }
                    else if (n.Contains("CDB") && n.Contains("EMPRESA"))
                    {
                        if (jurosCdbEmpresa == 0)
                            jurosCdbEmpresa = baseValor * taxaDiaria;
                        else
                            jurosCdbEmpresa += baseValor * taxaDiaria;
                    }
                }

                // persistir e marcar dia
                SaveJurosCdbToPrefs();
                Preferences.Default.Set(KEY_JUROS_CDB_ULTIMO_DIA, hoje);

                // atualizar labels/UI
                AtualizarLabelsJurosCdb();

                await Task.Delay(120); // pequena espera para UX
            }
            catch
            {
                // não falhar a UI
            }
        }
        private void AtualizarLabelsJurosCdb()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_labelGanhoCdbPessoal != null)
                        _labelGanhoCdbPessoal.Text = $"+ {jurosCdbPessoal:C2}";

                    if (_labelGanhoCdbEmpresa != null)
                        _labelGanhoCdbEmpresa.Text = $"+ {jurosCdbEmpresa:C2}";

                    // também atualiza dashboard financeiro para refletir os juros acumulados
                    AtualizarDashboardFinanceiro();
                }
                catch
                {
                    // não falhar a UI
                }
            });
        }       
        private async Task<bool> EnsureAlphaVantageKeyAsync()
        {
            var apiKey = Preferences.Default.Get("ALPHA_VANTAGE_KEY", "");
            if (!string.IsNullOrWhiteSpace(apiKey)) return true;

            // pede ao usuário a chave (apenas para debug / dev)
            var input = await DisplayPromptAsync("Alpha Vantage", "Informe sua API Key Alpha Vantage (cole aqui):", "Salvar", "Cancelar", placeholder: "");
            if (string.IsNullOrWhiteSpace(input))
            {
                await DisplayAlert("Chave ausente", "API key não informada. Configure 'ALPHA_VANTAGE_KEY' nas Preferences.", "OK");
                return false;
            }

            Preferences.Default.Set("ALPHA_VANTAGE_KEY", input.Trim());
            return true;
        }
        private async Task<double?> GetStockPriceAlphaVantageAsync(string symbol, int cacheSeconds = 60)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return null;

            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    System.Diagnostics.Debug.WriteLine("[AlphaV] Sem conectividade.");
                    return null;
                }
            }
            catch { }

            var apiKey = Preferences.Default.Get("ALPHA_VANTAGE_KEY", "T4JDHPOFERO7ZRIN");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                System.Diagnostics.Debug.WriteLine("[AlphaV] API key ausente.");
                return null;
            }

            // normaliza símbolo e prepara tentativas (com e sem .SA)
            symbol = symbol.Trim().ToUpperInvariant();
            var trySymbols = new List<string>();
            if (!symbol.EndsWith(".SA", StringComparison.OrdinalIgnoreCase))
            {
                trySymbols.Add(symbol + ".SA");
                trySymbols.Add(symbol);
            }
            else
            {
                trySymbols.Add(symbol);
                trySymbols.Add(symbol.Replace(".SA", ""));
            }

            foreach (var sym in trySymbols.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string cacheKeyPrice = $"stock_price_av_{sym}";
                string cacheKeyTs = $"stock_price_av_ts_{sym}";

                // cache simples
                if (cacheSeconds > 0)
                {
                    var tsStr = Preferences.Default.Get(cacheKeyTs, "");
                    if (DateTime.TryParse(tsStr, out var ts) && (DateTime.UtcNow - ts).TotalSeconds < cacheSeconds)
                    {
                        var cached = Preferences.Default.Get(cacheKeyPrice, "");
                        if (double.TryParse(cached, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p))
                            return p;
                    }
                }

                var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(sym)}&apikey={apiKey}";

                try
                {
                    var json = await _stockHttpClient.GetStringAsync(url);
                    System.Diagnostics.Debug.WriteLine($"[AlphaV] URL: {url}");
                    System.Diagnostics.Debug.WriteLine($"[AlphaV] JSON: {json}");

                    var jobj = Newtonsoft.Json.Linq.JObject.Parse(json);

                    // checar mensagens de limite / erro
                    var note = jobj["Note"]?.ToString();
                    var err = jobj["Error Message"]?.ToString();
                    if (!string.IsNullOrEmpty(note))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AlphaV] Note: {note}");
                        // não retryar agora — pode ser limite
                        return null;
                    }
                    if (!string.IsNullOrEmpty(err))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AlphaV] Error Message: {err}");
                        return null;
                    }

                    var quote = jobj["Global Quote"];
                    if (quote == null || !quote.HasValues)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AlphaV] Global Quote vazio para {sym}");
                        continue; // tenta próximo formato de símbolo
                    }

                    var priceToken = quote["05. price"] ?? quote["price"] ?? quote["05. Price"];
                    if (priceToken != null && double.TryParse(priceToken.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double price))
                    {
                        Preferences.Default.Set(cacheKeyPrice, price.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        Preferences.Default.Set(cacheKeyTs, DateTime.UtcNow.ToString("o"));
                        System.Diagnostics.Debug.WriteLine($"[AlphaV] {sym} -> {price}");
                        return price;
                    }

                    System.Diagnostics.Debug.WriteLine($"[AlphaV] campo de preço não encontrado no JSON para {sym}.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AlphaV] Exception ({sym}): {ex.Message}");
                    // continua tentando outros formatos de símbolo
                }
            }

            return null;
        }
        private void RegistrarCustoDiarioFundoIfNeeded()
        {
            try
            {
                string ultimo = Preferences.Default.Get(KEY_FUNDO_ULTIMO_DIA, "");
                string hoje = DateTime.Now.ToString("yyyy-MM-dd");
                if (ultimo != hoje)
                {
                    // adiciona R$100 ao fundo diariamente (persistir apenas o saldo e a data)
                    double total = Preferences.Default.Get(KEY_FUNDO_SALDO, 0.0);
                    total += 100.0;
                    Preferences.Default.Set(KEY_FUNDO_SALDO, total);
                    _fundoSaldo = total;
                    Preferences.Default.Set(KEY_FUNDO_ULTIMO_DIA, hoje);

                    // NÃO chamar SalvarInvestimentos() aqui — pode sobrescrever a lista de investimentos
                    // se ela ainda não tiver sido carregada em memória.
                }
            }
            catch
            {
                // ignorar falhas
            }
        }
        private async Task<double?> GetDailyStockPriceAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return null;

            // normaliza e gera keys seguras
            var s = symbol.Trim().ToUpperInvariant();
            var keyBase = $"stock_price_daily_{SanitizeKey(s)}";
            var keyPrice = keyBase;
            var keyDate = keyBase + "_date";

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // se já temos preço para hoje, retorna imediatamente
            try
            {
                var savedDate = Preferences.Default.Get(keyDate, "");
                var savedPriceStr = Preferences.Default.Get(keyPrice, "");
                if (savedDate == today && double.TryParse(savedPriceStr, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out double savedPrice))
                    return savedPrice;
            }
            catch
            {
                // ignora problemas de Preferences e tenta buscar
            }

            // tenta buscar via AlphaVantage (ou outro fallback que já implementou)
            try
            {
                double? fetched = await GetStockPriceAlphaVantageAsync(s, cacheSeconds: 5);
                if (fetched.HasValue)
                {
                    // salva preço e a data do fetch (UTC date)
                    Preferences.Default.Set(keyPrice, fetched.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Preferences.Default.Set(keyDate, today);
                    System.Diagnostics.Debug.WriteLine($"[GetDaily] {s} salvo {fetched.Value} ({today})");
                    return fetched.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetDaily] Ex ao buscar {s}: {ex.Message}");
            }

            // fallback: se fetch falhou, retorna qualquer valor previamente salvo (mesmo que de dia anterior)
            try
            {
                var oldPriceStr = Preferences.Default.Get(keyPrice, "");
                if (!string.IsNullOrEmpty(oldPriceStr) && double.TryParse(oldPriceStr, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out double oldPrice))
                {
                    System.Diagnostics.Debug.WriteLine($"[GetDaily] Usando preço em cache antigo para {s}: {oldPrice}");
                    return oldPrice;
                }
            }
            catch { }

            return null;
        }
        private void OrganizarInvestimentosPorCategoria(
            VerticalStackLayout layout,
            IEnumerable<dynamic> investments,
            Dictionary<string, double?> precoPorAtivo)
        {
            var stackRendaFixa = new VerticalStackLayout { Spacing = 8 };
            var stackAcoes = new VerticalStackLayout { Spacing = 8 };
            var stackFiis = new VerticalStackLayout { Spacing = 8 };

            layout.Add(new Label { Text = "Renda Fixa", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black });
            layout.Add(stackRendaFixa);

            layout.Add(new Label { Text = "Ações", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black });
            layout.Add(stackAcoes);

            layout.Add(new Label { Text = "FIIs", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.Black });
            layout.Add(stackFiis);

            foreach (var inv in investments.OrderBy(i => i.Name))
            {
                if (string.IsNullOrWhiteSpace(inv?.Name)) continue;

                var card = CriarCardDoInvestimento(inv, precoPorAtivo);
                string invNameUpper = (inv.Name ?? "").Trim().ToUpperInvariant();

                if (invNameUpper.Contains("CDB"))
                {
                    stackRendaFixa.Add(card);
                }
                else if (invNameUpper.Contains("BBSE3") || invNameUpper.Contains("ITUB4") || invNameUpper.Contains("PETR4"))
                {
                    stackAcoes.Add(card);
                }
                else
                {
                    stackFiis.Add(card);
                }
            }
        }

        #endregion

        // -------------------------TELAS------------------------------//

        #region Cards e Botões Gerais
        private View CriarCardPercentual(string emoji, string titulo, double percentual, Color corFundo)
        {
            var titleLabel = new Label
            {
                Text = $"{emoji} {titulo}",
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                TextColor = Colors.White
            };

            var pctLabel = new Label
            {
                Text = $"{percentual:N2}%",
                FontAttributes = FontAttributes.Bold,
                FontSize = 18,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.End
            };

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 10
            };
            grid.Add(titleLabel, 0, 0);
            grid.Add(pctLabel, 1, 0);

            return new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Background = corFundo,
                Padding = 12,
                Content = grid,
                Margin = new Thickness(0, 4)
            };
        }
        private View CriarBotaoAcao(string texto, string corHex, Func<Task> acao)
        {
            // Texto em uma linha, fonte responsiva
            var computedFont = ObterFonteResponsiva(texto, 16, 10);

            var lbl = new Label
            {
                Text = texto,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                FontSize = computedFont,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var border = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                Background = Color.FromArgb(corHex),
                Padding = 10,
                Content = lbl,
                MinimumWidthRequest = 100,
                HorizontalOptions = LayoutOptions.Fill
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await acao();
            border.GestureRecognizers.Add(tap);

            return border;
        }
        private View CriarCardEditavel(string icone, string titulo, double valor, Color corValor, Action aoClicar)
        {
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(GridLength.Star), new ColumnDefinition { Width = GridLength.Auto } },
                ColumnSpacing = 12
            };

            var icon = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Background = Color.FromArgb("#F1F5F9"),
                WidthRequest = 44,
                HeightRequest = 44,
                Content = new Label { Text = icone, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 28 }
            };

            var stack = new VerticalStackLayout { Spacing = 2 };
            stack.Children.Add(new Label { Text = titulo, FontSize = 13, TextColor = Colors.Gray }); // antes 12
            stack.Children.Add(new Label { Text = valor.ToString("C2"), FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = corValor }); // antes 20

            var btnEditar = new Button { Text = "✏️", BackgroundColor = Colors.Transparent, TextColor = Colors.Gray, CornerRadius = 8 };
            btnEditar.Clicked += (s, e) => aoClicar();

            grid.Add(icon, 0, 0);
            grid.Add(stack, 1, 0);
            grid.Add(btnEditar, 2, 0);

            return new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(14) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 12,
                Content = grid
            };
        }
        private View CriarCardNaoEditavel(string icone, string titulo, double valor, Color corValor)
        {
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 12
            };

            var icon = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Background = Color.FromArgb("#F1F5F9"),
                WidthRequest = 44,
                HeightRequest = 44,
                Content = new Label { Text = icone, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 28 }
            };

            var stack = new VerticalStackLayout { Spacing = 2 };
            stack.Children.Add(new Label { Text = titulo, FontSize = 13, TextColor = Colors.Gray });
            stack.Children.Add(new Label { Text = valor.ToString("C2"), FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = corValor });

            grid.Add(icon, 0, 0);
            grid.Add(stack, 1, 0);

            return new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(14) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 12,
                Content = grid
            };
        }
        private View CriarCardAdicionar(string icone, string titulo, double valor, Color corValor, Action aoClicar)
        {
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition(GridLength.Star), new ColumnDefinition { Width = GridLength.Auto } },
                ColumnSpacing = 12
            };

            var icon = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Background = Color.FromArgb("#F1F5F9"),
                WidthRequest = 44,
                HeightRequest = 44,
                Content = new Label { Text = icone, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontSize = 28 }
            };

            var stack = new VerticalStackLayout { Spacing = 2 };
            stack.Children.Add(new Label { Text = titulo, FontSize = 13, TextColor = Colors.Gray });
            stack.Children.Add(new Label { Text = valor.ToString("C2"), FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = corValor });

            var btnAdd = new Button { Text = "+", BackgroundColor = Colors.Transparent, TextColor = Colors.Gray, CornerRadius = 8, WidthRequest = 44, HeightRequest = 36 };
            btnAdd.Clicked += (s, e) => aoClicar();

            grid.Add(icon, 0, 0);
            grid.Add(stack, 1, 0);
            grid.Add(btnAdd, 2, 0);

            return new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(14) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 12,
                Content = grid
            };
        }
        private View CriarCardDoInvestimento(dynamic inv, Dictionary<string, double?> precoPorAtivo)
        {
            double? preco = precoPorAtivo.ContainsKey(inv.Name ?? "") ? precoPorAtivo[inv.Name ?? ""] : null;
            double priceNumeric = preco ?? inv.PricePerUnit;

            var nameLabel = new Label { Text = inv.Name, FontAttributes = FontAttributes.Bold, TextColor = Colors.DarkBlue };
            var qtyLabel = new Label { Text = $"Cotas: {inv.Quantity}", TextColor = Colors.DimGray };

            string marketPriceText;
            if (preco.HasValue)
                marketPriceText = preco.Value.ToString("C2");
            else if (inv.PricePerUnit > 0)
                marketPriceText = inv.PricePerUnit.ToString("C2") + " (salvo)";
            else
                marketPriceText = "Preço indisponível";

            var priceLabel = new Label { Text = $"Valor: {marketPriceText}", TextColor = Colors.DimGray };

            double totalBase = priceNumeric * inv.Quantity;
            string totalText = (priceNumeric > 0 && inv.Quantity > 0) ? (priceNumeric * inv.Quantity).ToString("C2") : "—";

            var valorTotal = new Label { Text = totalText, FontAttributes = FontAttributes.Bold, FontSize = 16, TextColor = Colors.Blue };

            // Variação percentual
            Label changeLabel = new Label { Text = "", FontAttributes = FontAttributes.Bold, FontSize = 14, VerticalOptions = LayoutOptions.Center };

            string invNameUpper = (inv.Name ?? "").Trim().ToUpperInvariant();

            if (invNameUpper.Contains("CDB"))
            {
                // Se for CDB, mostrar juros acumulados
                double juros = 0;
                if (invNameUpper.Contains("PESSOAL"))
                    juros = jurosCdbPessoal;
                else if (invNameUpper.Contains("EMPRESA"))
                    juros = jurosCdbEmpresa;

                double pctJuros = totalBase > 0 ? (juros / totalBase * 100) : 0;

                changeLabel.Text = $"(▲{pctJuros:N2}%) + {juros:C2} ";
                changeLabel.TextColor = Color.FromArgb("#2E7D32");
            }
            else if (inv.PricePerUnit > 0 && priceNumeric > 0)
            {
                // Para outros ativos, manter variação de mercado
                double previous = inv.PricePerUnit;
                double current = priceNumeric;
                double diff = current - previous;
                double pct = Math.Round((diff / previous) * 100.0, 2);

                if (pct > 0)
                {
                    changeLabel.Text = $"▲ +{pct:N2}%";
                    changeLabel.TextColor = Color.FromArgb("#2E7D32");
                }
                else if (pct < 0)
                {
                    changeLabel.Text = $"▼ {pct:N2}%";
                    changeLabel.TextColor = Color.FromArgb("#D32F2F");
                }
                else
                {
                    changeLabel.Text = $"— 0,00%";
                    changeLabel.TextColor = Colors.Gray;
                }
            }


            var corHistorico = Color.FromArgb("#455A64");
            var corVender = Color.FromArgb("#E53935");

            var btnHistorico = CriarBotaoEstilizado("HISTÓRICO", corHistorico, async () => await MostrarHistoricoInvestimento(inv));
            var btnVender = CriarBotaoEstilizado("VENDER", corVender, async () => { await VenderInvestimentoPrompt(inv); await AbrirInvestimentos(); });

            var rightBtns = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
            rightBtns.Add(btnHistorico);
            rightBtns.Add(btnVender);

            var nomeStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            nomeStack.Add(nameLabel);

            var leftStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            leftStack.Add(qtyLabel);
            leftStack.Add(priceLabel);

            var valorStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End };
            valorStack.Add(valorTotal);
            if (!string.IsNullOrEmpty(changeLabel.Text))
                valorStack.Add(changeLabel);




            var contentGrid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 12
            };
            contentGrid.Add(nomeStack, 0, 0);
            contentGrid.Add(leftStack, 0, 1);
            contentGrid.Add(valorStack, 1, 0);
            contentGrid.Add(rightBtns, 1, 1);

            var card = new Border
            {
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                Background = Colors.White,
                Stroke = Color.FromArgb("#ECEFF4"),
                Padding = 10,
                Content = contentGrid
            };

            return card;
        }
        private View CriarBotaoAtualizarComBadge(Func<Task> acaoAsync)
        {
            // botão base (mantém aparência existente)
            var botao = CriarBotaoEstilizado("ATUALIZAR", Color.FromArgb("#0D47A1"), () => _ = acaoAsync());

            // badge
            _badgeLabelAtualizar = new Label
            {
                Text = "0",
                TextColor = Colors.White,
                FontSize = 11,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            _badgeBorderAtualizar = new Border
            {
                Background = Colors.Red,
                Stroke = Colors.Red,
                StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(10) },
                Padding = new Thickness(6, 2),
                Content = _badgeLabelAtualizar,
                IsVisible = false,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, -8, -8, 0),
                ZIndex = 2
            };

            var container = new Grid();
            container.Add(botao, 0, 0);
            container.Add(_badgeBorderAtualizar, 0, 0);

            // garante estado inicial coerente
            AtualizarBadgePendentes();

            return container;
        }

        #endregion

        #region Https
        private class PendingHttpCommand
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Url { get; set; } = "";
            public string Method { get; set; } = "POST";
            public string JsonBody { get; set; } = "";
            public int Attempts { get; set; } = 0;
            public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");

            // metadata opcional — não executa nada localmente, apenas para rastrear
            public string? Operation { get; set; } = null;   // ex: "renovacao", "limpar_pendencia"
            public string? LocalRef { get; set; } = null;    // ex: nome do cliente ou id da NF
        }
        private async Task PausarClienteNaPlanilha(string nome)
        {
            var clienteNoApp = _listaCompletaServidor.FirstOrDefault(c => c.Cliente == nome);
            if (clienteNoApp == null) return;

            if (clienteNoApp.Pg == "R$ 100,00")
            {
                var dadosParaEnviar = new { cliente = nome, ativo = "NÃO", datapg = "", pg = "", situacao = "NOVO" };

                // executar alterações locais ANTES (ou independente) da tentativa HTTP
                clienteNoApp.IsPendente = false;
                _listaPendentesLocal.RemoveAll(x => x.Cliente == clienteNoApp.Cliente);
                SalvarPendentesNoDispositivo();

                // tentar enviar — se offline, enfileira apenas o HTTP
                var enviado = await PostJsonOrQueueAsync("https://kflmulti.com/AndroidStudio/AlteraPlanilha.php", dadosParaEnviar, operation: "pausa", localRef: nome);

                // limpar pendência remota também (não delegar re-execução local)
                await LimparPendenciaNaPlanilha(clienteNoApp.Cliente);
            }
            else
            {
                var dados = new { cliente = nome, ativo = "NÃO", situacao = "NOVO" };
                await PostJsonOrQueueAsync("https://kflmulti.com/AndroidStudio/AlteraPlanilha.php", dados, operation: "pausa", localRef: nome);
            }
        }
        public async Task EnviarDadosRenovacao(string nome, string planoC, bool pend, DateTime inicio, DateTime? datapg, DateTime fim, string v)
        {
            var corpo = new Dictionary<string, string>
            {
                { "cliente", nome },
                { "plano", planoC.Split('-')[0].Replace("Plano", "").Replace("Combo", "").Trim() },
                { "situacao", "RENOVA" },
                { "ativo", "OK" },
                { "inicio", inicio.ToString("dd/MM/yyyy") },
                { "fim", fim.ToString("dd/MM/yyyy") }
            };

            if (pend)
            {
                corpo.Add("datapg", datapg?.ToString("dd/MM/yyyy") ?? "");
                corpo.Add("pg", v);
            }
            else
            {
                corpo.Add("datapg", "");
                corpo.Add("pg", "");
            }

            // chame a API — se falhar, será enfileirado APENAS o HTTP com metadata
            await PostJsonOrQueueAsync("https://kflmulti.com/AndroidStudio/AlteraPlanilha.php", corpo, operation: "renovacao", localRef: nome);

            // OBS: o acumulado do custo de anúncios é atualizado no ponto onde a NF é criada localmente
        }
        private async Task<bool> PostFormToPlanilhaAsync(string url, Dictionary<string, string> form, string? operation = null, string? localRef = null, bool showAlert = false)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                using var formContent = new FormUrlEncodedContent(form);
                string raw = await formContent.ReadAsStringAsync();
                var content = new StringContent(raw, Encoding.UTF8, "application/x-www-form-urlencoded");
                var res = await client.PostAsync(url, content);
                var body = await res.Content.ReadAsStringAsync();

                // System.Diagnostics.Debug.WriteLine($"[AlteraPlanilha] POST {url} Status={(int)res.StatusCode} Body={body}");



                if (res.IsSuccessStatusCode) return true;

                // servidor respondeu erro -> enfileira somente o HTTP (raw form)
                _pendingHttpCommands.Add(new PendingHttpCommand
                {
                    Url = url,
                    Method = "POST",
                    JsonBody = raw,
                    Attempts = 1,
                    Operation = operation,
                    LocalRef = localRef
                });
                await SalvarPendingCommandsToPrefsAsync();
                AtualizarBadgePendentes();
                return false;
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"[AlteraPlanilha] EXCEPTION: {ex.Message}");
                try
                {
                    using var f = new FormUrlEncodedContent(form);
                    string raw = await f.ReadAsStringAsync();
                    _pendingHttpCommands.Add(new PendingHttpCommand
                    {
                        Url = url,
                        Method = "POST",
                        JsonBody = raw,
                        Attempts = 1,
                        Operation = operation,
                        LocalRef = localRef
                    });
                    await SalvarPendingCommandsToPrefsAsync();
                    AtualizarBadgePendentes();
                }
                catch { /* não falhar a UI */ }
                return false;
            }
        }
        private async Task<bool> PostJsonOrQueueAsync(string url, object body)
        {
            string json = JsonConvert.SerializeObject(body);
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync(url, content);
                if (res.IsSuccessStatusCode) return true;

                // servidor respondeu não-success -> enfileirar para tentar depois
                _pendingHttpCommands.Add(new PendingHttpCommand { Url = url, Method = "POST", JsonBody = json, Attempts = 1 });
                await SalvarPendingCommandsToPrefsAsync();
                AtualizarBadgePendentes();
                return false;
            }
            catch
            {
                // falha de rede/exceção -> enfileirar
                _pendingHttpCommands.Add(new PendingHttpCommand { Url = url, Method = "POST", JsonBody = json, Attempts = 1 });
                await SalvarPendingCommandsToPrefsAsync();
                AtualizarBadgePendentes();
                return false;
            }
        }
        private async Task TryEnviarComandosPendentesAsync()
        {
            // mostra loader enquanto tenta enviar pendentes
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _loader.IsVisible = true;
                    _loader.IsRunning = true;
                    _listView.Opacity = 0.3;
                    if (_floatBtn != null) _floatBtn.IsEnabled = false;
                }
                catch { }
            });

            try
            {
                if (_pendingHttpCommands == null || !_pendingHttpCommands.Any()) return;

                var enviados = new List<PendingHttpCommand>();
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                // itera sobre cópia para permitir remoção
                foreach (var cmd in _pendingHttpCommands.ToList())
                {
                    try
                    {
                        if (string.Equals(cmd.Method, "POST", StringComparison.OrdinalIgnoreCase))
                        {
                            var content = new StringContent(cmd.JsonBody, Encoding.UTF8, "application/json");
                            var res = await client.PostAsync(cmd.Url, content);
                            if (res.IsSuccessStatusCode)
                            {
                                enviados.Add(cmd);
                                continue;
                            }
                            else
                            {
                                cmd.Attempts++;
                            }
                        }
                        else
                        {
                            // suporte futuro a outros métodos
                            cmd.Attempts++;
                        }
                    }
                    catch
                    {
                        cmd.Attempts++;
                    }

                    // evita acumular indefinidamente
                    if (cmd.Attempts >= 20)
                    {
                        enviados.Add(cmd); // remover após muitas tentativas
                    }
                }

                if (enviados.Count > 0)
                {
                    foreach (var r in enviados) _pendingHttpCommands.Remove(r);
                    await SalvarPendingCommandsToPrefsAsync();
                    AtualizarBadgePendentes();
                }
            }
            catch
            {
                // não interromper fluxo principal se falhar
            }
            finally
            {
                // esconde loader e restaura UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _loader.IsRunning = false;
                        _loader.IsVisible = false;
                        _listView.Opacity = 1.0;
                        if (_floatBtn != null) _floatBtn.IsEnabled = true;
                        AtualizarBadgePendentes();
                    }
                    catch { }
                });
            }
        }
        public async Task LimparPendenciaNaPlanilha(string nome)
        {
            var dados = new { cliente = nome, datapg = "", pg = "" };
            await PostJsonOrQueueAsync("https://kflmulti.com/AndroidStudio/AlteraPlanilha.php", dados);
        }
        private async Task<bool> PostJsonOrQueueAsync(string url, object body, string? operation = null, string? localRef = null)
        {
            string json = JsonConvert.SerializeObject(body);
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await client.PostAsync(url, content);
                if (res.IsSuccessStatusCode) return true;

                // servidor respondeu não-success -> enfileirar apenas o HTTP (com metadata opcional)
                _pendingHttpCommands.Add(new PendingHttpCommand
                {
                    Url = url,
                    Method = "POST",
                    JsonBody = json,
                    Attempts = 1,
                    Operation = operation,
                    LocalRef = localRef
                });
                await SalvarPendingCommandsToPrefsAsync();
                AtualizarBadgePendentes();
                return false;
            }
            catch
            {
                // falha de rede/exceção -> enfileirar apenas o HTTP (com metadata opcional)
                _pendingHttpCommands.Add(new PendingHttpCommand
                {
                    Url = url,
                    Method = "POST",
                    JsonBody = json,
                    Attempts = 1,
                    Operation = operation,
                    LocalRef = localRef
                });
                await SalvarPendingCommandsToPrefsAsync();
                AtualizarBadgePendentes();
                return false;
            }
        }
        private void CarregarPendingCommandsFromPrefs()
        {
            try
            {
                var json = Preferences.Default.Get(KEY_PENDING_COMMANDS, "");
                if (!string.IsNullOrEmpty(json))
                    _pendingHttpCommands = JsonConvert.DeserializeObject<List<PendingHttpCommand>>(json) ?? new List<PendingHttpCommand>();
            }
            catch
            {
                _pendingHttpCommands = new List<PendingHttpCommand>();
            }
        }
        private async Task SalvarPendingCommandsToPrefsAsync()
        {
            try
            {
                // captura cópia da fila para evitar race conditions enquanto serializa
                var snapshot = (_pendingHttpCommands ?? new List<PendingHttpCommand>()).ToList();
                var json = JsonConvert.SerializeObject(snapshot);
                // executa a escrita em background para não bloquear a UI
                await Task.Run(() => Preferences.Default.Set(KEY_PENDING_COMMANDS, json));
            }
            catch
            {
                // não falhar a UI em caso de erro de persistência
            }
        }

        #endregion

        #region Relatórios e Fechamentos mensais
        private async Task EnviarRelatorioWhatsapp()
        {
            string mensagem = GerarRelatorioMensal();
            try
            {
                string url = $"https://api.whatsapp.com/send?text={Uri.EscapeDataString(mensagem)}";
                await Launcher.Default.OpenAsync(new Uri(url));
            }
            catch (Exception)
            {
                await Share.Default.RequestAsync(new ShareTextRequest { Text = mensagem });
            }
        }
        private async Task VerHistoricoMensal()
        {
            var mapaRelatorios = new Dictionary<string, string>();
            for (int i = 0; i < 12; i++)
            {
                var dataRef = DateTime.Now.AddMonths(-i);
                string chave = dataRef.ToString("MM_yyyy");
                if (Preferences.Default.ContainsKey($"relatorio_{chave}"))
                {
                    string nomeAmigavel = dataRef.ToString("MMMM / yyyy").ToUpper();
                    mapaRelatorios.Add(nomeAmigavel, chave);
                }
            }

            if (mapaRelatorios.Count == 0) { await DisplayAlert("Histórico", "Nenhum relatório automático foi salvo ainda.", "OK"); return; }

            string escolha = await DisplayActionSheet("Selecione o Relatório:", "Cancelar", null, mapaRelatorios.Keys.ToArray());
            if (escolha != "Cancelar" && mapaRelatorios.ContainsKey(escolha))
            {
                string chaveReal = mapaRelatorios[escolha];
                string conteudoRelatorio = Preferences.Default.Get($"relatorio_{chaveReal}", "");
                string acao = await DisplayActionSheet($"Relatório {escolha}", "Voltar", null, "Visualizar", "Enviar para WhatsApp");
                if (acao == "Visualizar") await DisplayAlert(escolha, conteudoRelatorio, "OK");
                else if (acao == "Enviar para WhatsApp") await Launcher.Default.OpenAsync($"https://api.whatsapp.com/send?text={Uri.EscapeDataString(conteudoRelatorio)}");
            }
        }
        private void VerificarFechamentoMensal()
        {
            var hoje = DateTime.Now.Date;

            // pega a última vez que rodou a limpeza
            var ultimaLimpezaStr = Preferences.Default.Get("ultimo_limpeza_mensal", "");
            DateTime ultimaLimpeza;

            // se já existe registro e foi feito hoje, não faz de novo
            if (DateTime.TryParse(ultimaLimpezaStr, out ultimaLimpeza))
            {
                if (ultimaLimpeza.Date == hoje)
                {
                    return; // já limpou hoje
                }
            }

            // se for dia 01, faz a limpeza e registra
            if (hoje.Day == 1)
            {
                LimparDadosMensais();
                Preferences.Default.Set("ultimo_limpeza_mensal", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }
        private void SalvarFechamentoMes()
        {
            string mesAnoChave = DateTime.Now.ToString("MM_yyyy");

            // gera relatório para o mês corrente
            string dadosFechamento = GerarRelatorioMensal(DateTime.Now);
            Preferences.Default.Set($"relatorio_{mesAnoChave}", dadosFechamento);
            Preferences.Default.Set($"relatorio_salvo_{mesAnoChave}", true);

            // salva snapshot das NFs do mês atual para referência futura
            try
            {
                Preferences.Default.Set($"nfs_mes_{mesAnoChave}", JsonConvert.SerializeObject(_listaNfLocal ?? new List<NfModel>()));
            }
            catch
            {
                // não falhar o fechamento se persistência der problema
            }

            // salva snapshot do acumulado de anúncios deste mês
            double acumuladoAnuncios = Preferences.Default.Get(KEY_CUSTO_ANUNCIOS_MES, 0.0);
            Preferences.Default.Set($"custo_anuncios_mes_{mesAnoChave}", acumuladoAnuncios);

            // Calcula imposto do mês que foi fechado (mês atual) e adiciona como gasto fixo no dia 20
            try
            {
                var imposto = CalcularImpostoParaMes(DateTime.Now);
                AdicionarImpostoFixoParaMes(DateTime.Now, imposto);
            }
            catch
            {
                // não falhar o fechamento se houver problema no cálculo/persistência
            }

            // limpa lista de NFs do mês (inicia nova lista para o próximo mês)
            _listaNfLocal.Clear();
            try { Preferences.Default.Set("lista_nf_salva", JsonConvert.SerializeObject(_listaNfLocal)); } catch { }

            // zera acumulados mensais relevantes
            LimparDadosMensais();
        }
        private void SalvarFechamentoMesRetroativo(DateTime data)
        {
            string mesAnoChave = data.ToString("MM_yyyy");

            // Tenta recuperar snapshot de NFs desse mês, se existir
            List<NfModel> nfsParaMes = new List<NfModel>();
            try
            {
                var jsonSnap = Preferences.Default.Get($"nfs_mes_{mesAnoChave}", "");
                if (!string.IsNullOrEmpty(jsonSnap))
                {
                    nfsParaMes = JsonConvert.DeserializeObject<List<NfModel>>(jsonSnap) ?? new List<NfModel>();
                }
                else
                {
                    // fallback: filtra a lista atual por data do mês solicitado
                    nfsParaMes = (_listaNfLocal ?? Enumerable.Empty<NfModel>())
                        .Where(n =>
                        {
                            var dt = ParseNfDate(n.Data);
                            return dt != null && dt.Value.Month == data.Month && dt.Value.Year == data.Year;
                        })
                        .ToList();

                    // persiste snapshot para referência futura
                    Preferences.Default.Set($"nfs_mes_{mesAnoChave}", JsonConvert.SerializeObject(nfsParaMes));
                }
            }
            catch
            {
                nfsParaMes = new List<NfModel>();
            }

            // Gera relatório usando os NFs selecionados (criamos conteúdo manualmente para não depender de _listaNfLocal)
            var ativosOk = _listaCompletaServidor.Where(c => c.Ativo?.Trim().ToLower() == "ok").ToList();
            double totalFaturamentoAtivos = ativosOk.Sum(c => ObterValorPorNomePlano(c.Plano));
            double totalNfMes = nfsParaMes.Sum(n => ParseValor(n.Valor));
            double receitaBruta = totalFaturamentoAtivos + totalNfMes;
            double impostos = Math.Round(receitaBruta * 0.06, 2);

            var resumo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cli in ativosOk)
            {
                string nomeP = (cli.Plano ?? "OUTROS").ToUpper().Trim();
                if (resumo.ContainsKey(nomeP)) resumo[nomeP]++;
                else resumo[nomeP] = 1;
            }

            string texto = $"📊 *RELATÓRIO FINANCEIRO* - {data:MM/yyyy}\n\n";
            texto += $"✅ *Total Ativos:* {ativosOk.Count}\n\n";
            foreach (var item in resumo) texto += $"• {item.Key}: {item.Value}\n";
            texto += $"\n💰 *Faturamento (base ativos):* R$ {totalFaturamentoAtivos:N2}";
            texto += $"\n🧾 *Entrada por NFs (mês):* R$ {totalNfMes:N2}";
            texto += $"\n🧾 *Imposto (6% sobre receita bruta):* R$ {impostos:N2}";
            double meuAnuncioTotal = Preferences.Default.Get("meu_anuncio_total_mes", 0.0);
            texto += $"\n📣 *Custo Meu Anúncio (mês):* R$ {meuAnuncioTotal:N2}";
            texto += $"\n\n_Gerado automaticamente pelo App_";

            Preferences.Default.Set($"relatorio_{mesAnoChave}", texto);

            // salva snapshot do acumulado de anúncios para esse mês (se aplicável)
            double acumuladoAnuncios = Preferences.Default.Get(KEY_CUSTO_ANUNCIOS_MES, 0.0);
            Preferences.Default.Set($"custo_anuncios_mes_{mesAnoChave}", acumuladoAnuncios);

            // adiciona imposto como gasto fixo para o mês retroativo (vencimento dia 20)
            try
            {
                var imposto = CalcularImpostoParaMes(data);
                AdicionarImpostoFixoParaMes(data, imposto);
            }
            catch
            {
                // não falhar se houver problema no cálculo/persistência
            }

            // persiste snapshot das despesas variáveis daquele mês (se ainda não salvo)
            try
            {
                string chaveVarsMes = $"fin_var_expenses_{mesAnoChave}";
                if (!Preferences.Default.ContainsKey(chaveVarsMes))
                    Preferences.Default.Set(chaveVarsMes, JsonConvert.SerializeObject(_variableExpenses));
            }
            catch { }
        }
        private void LimparDadosMensais()
        {
            // Limpa NFs/atividades do mês atual e persiste a lista vazia
            _listaNfLocal.Clear();
            try { Preferences.Default.Set("lista_nf_salva", JsonConvert.SerializeObject(_listaNfLocal)); } catch { }

            // limpa outras listas mensais persistentes (mantive comportamento atual)
            Preferences.Default.Remove("relatorio_mensal");

            // limpa gastos variáveis (recomeçam a cada mês)
            _variableExpenses.Clear();
            _variableExpensesReds.Clear();
            Preferences.Default.Set("despesas_cigarro_mes", 0.0);
            Preferences.Default.Set("despesas_mercado_mes", 0.0);
            Preferences.Default.Set("despesas_combustivel_mes", 0.0);

            // não zera imposto, apenas mantém o último salvo
            double impostoAnterior = Preferences.Default.Get("imposto_mes_anterior", 0.0);


            // mantém gastos fixos cadastrados, mas "desmarca" os vistos/included
            foreach (var f in _fixedExpenses)
                f.Included = false;

            // persiste alterações de despesas
            SalvarDespesasFinancas();

            // limpa acumulado do meu anúncio
            Preferences.Default.Set("meu_anuncio_total_mes", 0.0);
            Preferences.Default.Remove("meu_anuncio_ultimo_dia");

            // limpa acumulado de custo de anúncios do mês
            Preferences.Default.Set(KEY_CUSTO_ANUNCIOS_MES, 0.0);

            AtualizarDashboardFinanceiro();
            Preferences.Default.Set("ultimo_limpeza_mensal", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            Preferences.Default.Set(KEY_TOTAL_NF_MES, 0.0);
            _totalNfMesPersistido = 0.0;
        }
        private double CalcularImpostoParaMes(DateTime mesRef)
        {
            // 1) tentativa de ler imposto a partir do relatório salvo para o mês (se existir)
            try
            {
                string chaveRel = $"relatorio_{mesRef:MM_yyyy}";
                if (Preferences.Default.ContainsKey(chaveRel))
                {
                    string rel = Preferences.Default.Get(chaveRel, "");
                    if (!string.IsNullOrWhiteSpace(rel))
                    {
                        // procura por uma linha que contenha "Imposto" seguida de "R$ <valor>"
                        var m = Regex.Match(rel, @"Imposto.*?:\s*R\$\s*([\d\.,]+)", RegexOptions.IgnoreCase);
                        if (m.Success && m.Groups.Count > 1)
                        {
                            string valorStr = m.Groups[1].Value;
                            double parsed = ParseValor("R$ " + valorStr);
                            if (parsed > 0) return Math.Round(parsed, 2);
                        }

                        // também tenta achar formatos com emoji / markdown (caso o relatório tenha outro formato)
                        var m2 = Regex.Match(rel, @"\*\s*Imposto.*?:\*\s*R\$\s*([\d\.,]+)", RegexOptions.IgnoreCase);
                        if (m2.Success && m2.Groups.Count > 1)
                        {
                            string valorStr = m2.Groups[1].Value;
                            double parsed = ParseValor("R$ " + valorStr);
                            if (parsed > 0) return Math.Round(parsed, 2);
                        }
                    }
                }
            }
            catch
            {
                // se falhar no parsing do relatório, segue para cálculo tradicional
            }

            // 2) fallback: calcula a base a partir do estado atual em memória (comportamento anterior)
            double baseAtivos = _listaCompletaServidor
                .Where(c => c.Ativo?.Trim().ToLower() == "ok")
                .Sum(c => ObterValorPorNomePlano(c.Plano));

            double totalNfMes = 0;
            foreach (var nf in _listaNfLocal ?? Enumerable.Empty<NfModel>())
            {
                var dt = ParseNfDate(nf.Data);
                if (dt == null) continue;
                if (dt.Value.Month == mesRef.Month && dt.Value.Year == mesRef.Year)
                    totalNfMes += ParseValor(nf.Valor);
            }

            double faturamentoTotal = GetTotalNfMesPersistido();
            double imposto = Math.Round(faturamentoTotal * 0.06, 2);
            return imposto;
        }
        private void AdicionarImpostoFixoParaMes(DateTime mesRef, double imposto)
        {
            if (imposto <= 0) return;
            string nome = $"Imposto {mesRef:MM/yyyy}";

            // evita duplicação: procura por mesmo nome e mesmo dia 20
            bool existe = _fixedExpenses.Any(f =>
                string.Equals(f.Name?.Trim(), nome, StringComparison.OrdinalIgnoreCase) &&
                f.DayOfMonth == 20);

            if (existe) return;

            // calcula data de vencimento (garante dia 20 válido no mês)
            int diaVenc = Math.Min(20, DateTime.DaysInMonth(mesRef.Year, mesRef.Month));
            var dataVencimento = new DateTime(mesRef.Year, mesRef.Month, diaVenc);

            var impostoFixo = new FixedExpense
            {
                Name = nome,
                DayOfMonth = 20,
                Value = imposto,
                // marcar Included = true se a data de vencimento já passou (ou é hoje),
                // assim o imposto entra no totalFixedIncluded e afeta os cálculos de receita
                Included = dataVencimento <= DateTime.Now.Date
            };

            _fixedExpenses.Add(impostoFixo);
            SalvarDespesasFinancas();
        }
        private void EnsureImpostoAnteriorExists()
        {
            // pega o valor salvo do imposto do mês anterior
            double impostoMesAnterior = Preferences.Default.Get("imposto_mes_anterior", 0.0);

            // procura se já existe um custo fixo chamado "Imposto"
            var impostoFixo = _fixedExpenses.FirstOrDefault(f =>
                !string.IsNullOrWhiteSpace(f.Name) &&
                f.Name.Trim().Equals("Imposto", StringComparison.OrdinalIgnoreCase));

            if (impostoFixo == null)
            {
                // se não existe, cria um único imposto fixo
                _fixedExpenses.Add(new FixedExpense
                {
                    Name = "Imposto",
                    Value = impostoMesAnterior,
                    Included = true,
                    DayOfMonth = 20 // ou o dia que você quiser como padrão
                });
            }
            else
            {
                // se já existe, apenas atualiza o valor
                impostoFixo.Value = impostoMesAnterior;
                impostoFixo.Included = true;
            }

            // salva para garantir persistência
            SalvarDespesasFinancas();
        }
        private void VerificarResetDiarioEntradaHoje()
        {
            try
            {
                string ultimo = Preferences.Default.Get("entrada_hoje_ultimo_dia", "");
                string hoje = DateTime.Now.ToString("yyyy-MM-dd");
                if (ultimo != hoje)
                {
                    // limpa NFs do dia (entrada hoje) e persiste limpeza
                    //_listaNfLocal.Clear();
                    //Preferences.Default.Remove("lista_nf_salva");
                    Preferences.Default.Set("entrada_hoje_ultimo_dia", hoje);
                }
            }
            catch
            {
                // não falhar se Preferences der problema
            }
        }

        #endregion

        #region Calculos
        private string AjustarValorNf(string valorOriginal)
        {
            if (string.IsNullOrWhiteSpace(valorOriginal)) return "R$ 0,00";
            string apenasNumeros = Regex.Replace(valorOriginal, @"[^\d,\.]", "").Replace(".", ",").Trim();
            if (apenasNumeros == "100" || apenasNumeros == "100,00") return "R$ 150,00";
            return apenasNumeros.Contains("R$") ? apenasNumeros : $"R$ {apenasNumeros}";
        }
        private double ObterValorPorNomePlano(string nomePlano)
        {
            string p = nomePlano?.ToUpper().Trim() ?? "";
            if (p.Contains("GOLD ADVANCED")) return 600.00;
            if (p.Contains("GOLD SMART")) return 300.00;
            if (p.Contains("GOLD") || p.Contains("100")) return 150.00;
            if (p.Contains("MEGA ADVANCED")) return 1200.00;
            if (p.Contains("MEGA SMART")) return 600.00;
            if (p.Contains("MEGA")) return 300.00;
            if (p.Contains("PLUS ADVANCED")) return 2400.00;
            if (p.Contains("PLUS SMART")) return 1200.00;
            if (p.Contains("PLUS")) return 600.00;
            return 0;
        }
        private int ObterDiasPorPlano(string plano)
        {
            var p = (plano ?? "").ToUpperInvariant();
            if (p.Contains("SMART")) return 15;
            if (p.Contains("ADVANCED")) return 30;
            return 7;
        }
        private string ValorOuPlano(string valorOriginal, string plano)
        {
            // tenta obter valor numérico; se for zero ou inválido, usa o valor do plano
            double parsed = 0;
            try { parsed = ParseValor(valorOriginal); } catch { parsed = 0; }
            if (parsed <= 0) parsed = ObterValorPorNomePlano(plano);
            return $"R$ {parsed:N2}";
        }
        private DateTime? ParseNfDate(string dataStr)
        {
            if (string.IsNullOrWhiteSpace(dataStr)) return null;
            try
            {
                string s = dataStr.Replace("\\", "").Trim();
                if (s.Count(ch => ch == '/') == 1) s = s + "/" + DateTime.Now.Year.ToString();
                if (DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
                if (DateTime.TryParse(s, out var dt2)) return dt2;
            }
            catch { }
            return null;
        }

        #endregion

        #region Utilitarios
        private void AtualizarBadgePendentes()
        {
            // Pode ser chamado de qualquer thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                int count = _pendingHttpCommands?.Count ?? 0;
                if (_badgeBorderAtualizar == null || _badgeLabelAtualizar == null) return;

                if (count > 0)
                {
                    _badgeLabelAtualizar.Text = count.ToString();
                    _badgeBorderAtualizar.IsVisible = true;
                }
                else
                {
                    _badgeBorderAtualizar.IsVisible = false;
                }
            });
        }
        private async Task ExecutarComLoader(Func<Task> tarefa)
        {
            try
            {
                _loader.IsVisible = true;
                _loader.IsRunning = true;
                _listView.Opacity = 0.3;

                await tarefa();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro", ex.Message, "OK");
            }
            finally
            {
                _loader.IsRunning = false;
                _loader.IsVisible = false;
                _listView.Opacity = 1.0;
            }
        }
        private string? GetEmojiFontFamily()
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                return "Segoe UI Emoji";

            if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                return "Apple Color Emoji";

            if (DeviceInfo.Platform == DevicePlatform.iOS)
                return "AppleColorEmoji";

            // Android
            return null;
        }
        private async Task ShowTemporaryNotification(string message, int durationMs = 1400)
        {
            try
            {
                var badge = new Border
                {
                    Background = Color.FromArgb("#323232"),
                    Stroke = Color.FromArgb("#323232"),
                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                    Padding = new Thickness(12, 8),
                    Content = new Label { Text = message, TextColor = Colors.White, FontSize = 14, HorizontalTextAlignment = TextAlignment.Center },
                    Opacity = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(12, 24, 12, 0),
                    ZIndex = 999
                };

                // Adiciona sobre a tela atual
                _containerConteudo.Children.Add(badge);

                // anima entrada, espera e anima saída
                await badge.FadeTo(1, 150, Easing.CubicIn);
                await Task.Delay(durationMs);
                await badge.FadeTo(0, 180, Easing.CubicOut);

                _containerConteudo.Children.Remove(badge);
            }
            catch
            {
                // não falhar a UI
            }
        }
        private async Task InitializeNotificationsAsync()
        {
            try
            {
                // Android 13+ e iOS: pedir permissão (o plugin lida com plataformas que não precisam)
                await LocalNotificationCenter.Current.RequestNotificationPermission();

                await ScheduleDailyAt23Async();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeNotificationsAsync failed: {ex}");
            }
        }
        private async Task ScheduleDailyAt23Async()
        {
            try
            {
                // usar DateTimeOffset para calcular hora local corretamente e preservar offset
                var now = DateTimeOffset.Now;
                var today23 = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 0, 0, now.Offset);
                var firstRun = now <= today23 ? today23 : today23.AddDays(1);

                var request = new NotificationRequest
                {
                    NotificationId = 2300,
                    Title = "Fechamento",
                    Description = "Gerar Relatório",
                    Schedule = new NotificationRequestSchedule
                    {
                        NotifyTime = firstRun.DateTime,
                        RepeatType = NotificationRepeat.Daily
                    },
                    Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                    {
                        // Adicione o .AndroidOption antes do AndroidIcon
                        IconSmallName = new Plugin.LocalNotification.AndroidOption.AndroidIcon("noti"),
                        ChannelId = "daily_channel",


                    }

                };


                await LocalNotificationCenter.Current.Show(request);

                // exibir confirmação na UI thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        // await DisplayAlert("Definido", $"Notificação agendada para: {firstRun:yyyy-MM-dd HH:mm}", "OK");
                    }
                    catch { /* evitar que falha de UI quebre o fluxo */ }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleDailyAt23Async failed: {ex}");
            }
        }
        private string LimparNomePlano(string planoBruto)
        {
            if (string.IsNullOrWhiteSpace(planoBruto)) return "";

            // 1. Remove os prefixos conhecidos
            string nomeLimpo = planoBruto.Replace("Plano ", "").Replace("Combo ", "");

            // 2. Pega apenas o que vem antes do " - " (hífen)
            if (nomeLimpo.Contains("-"))
            {
                nomeLimpo = nomeLimpo.Split('-')[0].Trim();
            }

            return nomeLimpo; // Retornará "GOLD", "GOLD SMART", etc.
        }
        private async Task ClearAppCacheAsync()
        {
            try
            {
                // 1) Remover chaves conhecidas de Preferences (sem mexer em investimentos)
                var keys = new[]
                {
            "lista_clientes_cache", "lista_clientes_cache_prev", "backup_vistos_hoje", "data_vistos",
            "lista_pendentes_salva", "lista_nf_salva", "relatorio_mensal", "meu_anuncio_total_mes", "meu_anuncio_ultimo_dia",
            "meu_anuncio_ativo", "saldo_dia", "cartao_dia", "saldo_pessoal","cartao_pessoal","saldo_empresa","cartao_empresa",
            "total_clientes_ontem","data_ultima_meta","ultimo_limpeza_mensal",
            KEY_FIXED, KEY_VAR, KEY_CUSTO_POR_DIA, KEY_CUSTO_ANUNCIOS_MES,
            KEY_PAUSADOS_HOJE, KEY_RENOVADOS_HOJE, KEY_NOVOS_HOJE, KEY_RETORNADOS_HOJE, KEY_PENDENTES_PAGOS, KEY_TOTAL_NF_MES, KEY_PENDING_COMMANDS
        };

                foreach (var k in keys)
                {
                    try { Preferences.Default.Remove(k); } catch { }
                }

                // ⚠️ IMPORTANTE: não usar Preferences.Default.Clear()
                // pois isso apagaria também KEY_INVESTMENTS e KEY_FUNDO_SALDO (investimentos).

                // 2) Excluir arquivos em AppData e Cache (persistência local)
                try
                {
                    var appData = FileSystem.AppDataDirectory;
                    if (!string.IsNullOrEmpty(appData) && System.IO.Directory.Exists(appData))
                    {
                        foreach (var f in System.IO.Directory.GetFiles(appData, "*", System.IO.SearchOption.TopDirectoryOnly))
                        {
                            try { System.IO.File.Delete(f); } catch { }
                        }
                    }

                    var cacheDir = FileSystem.CacheDirectory;
                    if (!string.IsNullOrEmpty(cacheDir) && System.IO.Directory.Exists(cacheDir))
                    {
                        foreach (var f in System.IO.Directory.GetFiles(cacheDir, "*", System.IO.SearchOption.TopDirectoryOnly))
                        {
                            try { System.IO.File.Delete(f); } catch { }
                        }
                    }
                }
                catch { /* não falhar se IO der problema */ }

                // 3) Limpar estados em memória usados pelo app (sem mexer em investimentos)
                _listaCompletaServidor?.Clear();
                _listaAtivosOk?.Clear();
                _listaPendentesLocal?.Clear();
                _listaVenceHoje?.Clear();
                _listaRenovadosHoje?.Clear();
                _listaNfLocal?.Clear();
                _fixedExpenses?.Clear();
                _variableExpenses?.Clear();
                _variableExpensesReds?.Clear();
                ClientesExibidos?.Clear();

                // ⚠️ não limpar _listaInvestimentosLocal nem saldo do fundo

                // Atualizar UI rapidamente
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _searchEntry.Text = string.Empty;
                    ExecutarBuscaReal();
                });

                await DisplayAlert("Cache", "Cache e dados locais removidos (Investimentos preservados). Reinicie o app para garantir estado totalmente limpo.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro ao limpar cache", ex.Message, "OK");
            }
        }

        #endregion


    }

}

