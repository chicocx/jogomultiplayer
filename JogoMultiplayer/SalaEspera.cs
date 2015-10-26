using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JogoMultiplayer
{
    public partial class SalaEspera : Form
    {
        private Dictionary<string, Jogador> jogadoresMapa = new Dictionary<string, Jogador>();

        private Boolean conectado;

        public SalaEspera()
        {
            InitializeComponent();
        }

        private void tratarMsg02(byte[] retorno)
        {

            string stringData = Encoding.ASCII.GetString(retorno);

            int tipoMsg = Int32.Parse(stringData.Substring(0, 2));
            int tamanhoMsg = Int32.Parse(stringData.Substring(2, 3));
            String mensagem = stringData.Substring(5, tamanhoMsg - 5);

            String[] dados = mensagem.Split('|');

            if (!jogadoresMapa.ContainsKey(dados[0]))
            {
                Jogador jog = new Jogador();
                jog.apelido = dados[0];
                jog.nome = dados[1];
                jog.confirmado = true;
                jogadoresMapa.Add(jog.apelido, jog);
            }
            else
            {
                Jogador jog = jogadoresMapa[dados[0]];
                jog.confirmado = true;
            }

        }

        private int getIdxLista(String nick)
        {
            int idx = 0;
            foreach(String txt in jogadores.Items)
            {
                if (txt.Contains(nick))
                {
                    return idx;
                }
                idx++;
            }
            return -1;
        }

        private void SalaEspera_Load(object sender, EventArgs e)
        {
            Thread threadAtualizadoraUsuarios = new Thread(()=> {
                while (true)
                {
                    List<String> apelidosRemover = new List<String>();

                    this.Invoke((MethodInvoker)(() => jogadores.Items.Clear()));
                    foreach (var item in jogadoresMapa)
                    {
                        Jogador jog = item.Value;
                        var diffInSeconds = (DateTime.Now - jog.momento).TotalSeconds;
                        String texto = jog.apelido + " - " + jog.nome + (jog.confirmado ? " (CONFIRMADO)" : "");
                        if (diffInSeconds > 10)
                        {
                            apelidosRemover.Add(jog.apelido);
                        }
                        else if (!jogadores.Items.Contains(texto))
                        {
                            this.Invoke((MethodInvoker)(() => jogadores.Items.Add(texto)));
                        }

                    }
                    foreach(string appRev in apelidosRemover)
                    {
                        jogadoresMapa.Remove(appRev);
                    }
                    Thread.Sleep(1000);
                }

            });
            threadAtualizadoraUsuarios.Start();

            Thread threadLeitoraBroadcast = new Thread(() => {
                Socket sock = new Socket(AddressFamily.InterNetwork,
                                      SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint iep = new IPEndPoint(IPAddress.Any, 20152);
                sock.Bind(iep);
                EndPoint ep = (EndPoint)iep;

                while (true)
                {
                    byte[] data = new byte[1024];
                    int recv = sock.ReceiveFrom(data, ref ep);
                    string stringData = Encoding.ASCII.GetString(data, 0, recv);

                    int tipoMsg = Int32.Parse(stringData.Substring(0, 2));
                    int tamanhoMsg = Int32.Parse(stringData.Substring(2, 3));
                    String mensagem = stringData.Substring(5, tamanhoMsg - 5);

                    String[] dados = mensagem.Split('|');

                    switch (tipoMsg)
                    {
                        case 1:
                            if (!jogadoresMapa.ContainsKey(dados[0]))
                            {
                                Jogador jog = new Jogador();
                                jog.apelido = dados[0];
                                jog.nome = dados[1];
                                jogadoresMapa.Add(jog.apelido, jog);
                            }
                            else
                            {
                                Jogador jog = jogadoresMapa[dados[0]];
                                jog.momento = DateTime.Now;
                            }

                            if (conectado)
                            {
                                String nick = txtApelido.Text;
                                String nome = txtNome.Text;
                                int tamanho = 6 + nick.Length + nome.Length;
                                String mensagemEnvio = String.Format("02{0:D3}{1}|{2}", tamanho, nick, nome);
                                byte[] bytes = Encoding.ASCII.GetBytes(mensagemEnvio);
                                sock.SendTo(bytes, 0, bytes.Length, SocketFlags.None, ep);
                                
                            }
                            break;
                        case 2:
                            tratarMsg02(data);
                            break;
                    }

                }

                sock.Close();


            });
            threadLeitoraBroadcast.Start();

        }

        private void jogadores_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnConectar_Click(object sender, EventArgs e)
        {
            if (!conectado)
            {
                conectado = true;
                Thread threadEnvioBroadcast = new Thread(() => {
                    while (conectado)
                    {
                        String nick = txtApelido.Text;
                        String nome = txtNome.Text;
                        int tamanho = 6 + nick.Length + nome.Length;
                        String mensagem = String.Format("01{0:D3}{1}|{2}", tamanho, nick, nome);
                        UdpClient client = new UdpClient();
                        IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, 20152);
                        byte[] bytes = Encoding.ASCII.GetBytes(mensagem);
                        client.Send(bytes, bytes.Length, ip);

                        byte[] retorno = client.Receive(ref ip);
                        tratarMsg02(retorno);

                        client.Close();
                        Thread.Sleep(1000);
                    }
                });
                threadEnvioBroadcast.Start();
                btnConectar.Text = "Desconectar";
            }
            else
            {
                conectado = false;
                btnConectar.Text = "Conectar";
            }
        }
    }

    class Jogador
    {
        public String apelido;

        public String nome;

        public DateTime momento = DateTime.Now;

        public Boolean confirmado;

    }

}
