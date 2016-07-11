using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FeelTheSpace
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public List<MediaElement> sounds = new List<MediaElement>();

        public RfcommDeviceService _service;
        public StreamSocket _socket;
        public DataReader dataReaderObject;
        ObservableCollection<PairedDeviceInfo> _pairedDevices;


        public CancellationTokenSource ReadCancellationTokenSource;


        public MainPage()
        {
            this.InitializeComponent();

            //Metoda koja sve zvukove stavi u prethodno kreiranu listu zvukova
            InitializeSounds();

            //Metoda koja pronalazi sve dostupne uparene Bluetooth module
            //i stavlja ih u kolekciju potencijalnih uredjaja za uparivanje
            InitializeRfcommDeviceService();
        }


        public async Task BluetoothConection()
        {
            await Task.Delay(2000);
            CheckBluetooth.Play();
            await Task.Delay(3500);
            CoreApplication.Exit();
        }

        private async void InitializeSounds()
        {
            await PlayIntro();  
        }

        public async Task PlayIntro()
        {
            await Task.Delay(2000);
            right_intro.Play();
            while (right_intro.CurrentState == MediaElementState.Playing)
                continue;
            await Task.Delay(1000);
            left_intro.Play();

            await Task.Delay(1500);

        }

        private async void InitializeRfcommDeviceService()
        {
            await Task.Delay(3000);
            try
            {
                //Pronadji sve uredjaje koji su dostupni putem bluetooth-a
                DeviceInformationCollection DeviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));


                var numDevices = DeviceInfoCollection.Count();

                //kreiraj praznu kolekciju uparenih uredjaja
                _pairedDevices = new ObservableCollection<PairedDeviceInfo>();
                _pairedDevices.Clear();

                if (numDevices == 0)
                {
                    // Debug.WriteLine("InitializeRfcommDeviceService: No paired devices found.");
                    MessageDialog md = new MessageDialog("InitializeRfcommDeviceService: No paired devices found.", "No devices");
                }
                else
                {
                    // Nadjeni upareni uredjaji
                    foreach (var deviceInfo in DeviceInfoCollection)
                    {
                        _pairedDevices.Add(new PairedDeviceInfo(deviceInfo));
                    }
                }
                PairedDevices.Source = _pairedDevices;
                //Debug.WriteLine(_pairedDevices[0].Name);

                //Metoda koja iz prethodno napunjene kolekcije potencijalnih konekcija
                //pokusava uspostaviti konekciju sa nasim uredjajem
                InitializeHC05Connection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InitializeRfcommDeviceService: " + ex.Message);
            }

        }

        private async void InitializeHC05Connection()
        {
            DeviceInformation DeviceInfo = null;
            PairedDeviceInfo pairedDevice = null;

            //Prodji kroz uparene uredjaje i probaj naci HC-05 bluetooth modul
            for (int i = 0; i < _pairedDevices.Count(); i++)
            {
                if (_pairedDevices[i].Name == "HC-05")
                {
                    pairedDevice = _pairedDevices[i];
                    DeviceInfo = pairedDevice.DeviceInfo;
                    break;
                }
            }


            bool success = true;
            try
            {
                //Kreiraj servis koristeci ID HC-05 modula (ako je uspjesno nadjen)
                _service = await RfcommDeviceService.FromIdAsync(DeviceInfo.Id);

                if (_socket != null)
                {
                    // Ako vec postoji nekih stvari u socket-u, prvo ocisti sve resurse
                    _socket.Dispose();
                }

                _socket = new StreamSocket();
                try
                {
                    // Ako je bilo koji od dva parametra null ili prazan, ovaj poziv ce baciti izuzetak
                    await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);
                }
                catch (Exception ex)
                {
                    success = false;
                    BluetoothConection();
                   // Debug.WriteLine("Connect:" + ex.Message);
                }
                if (success)
                {
                    string msg = String.Format("Connected to {0}!", _socket.Information.RemoteAddress.DisplayName);
                   // Debug.WriteLine(msg);
                    //Metoda koja aktivira slusanje
                    Listen();
                }
            }
            catch (NullReferenceException ex)
            {
                // var dialog = new MessageDialog("Provjerite da li vam je uključen bluetooth na uređaju");
                // await dialog.ShowAsync();
                BluetoothConection();
            }
            catch (Exception ex)
            {
                //Debug.WriteLine("Overall Connect: " + ex.Message);
                _socket.Dispose();
                _socket = null;
            }
        }

        private async void Listen()
        {

            try
            {

                ReadCancellationTokenSource = new CancellationTokenSource();
                if (_socket.InputStream != null)
                {
                    dataReaderObject = new DataReader(_socket.InputStream);
                    // Nastavi citati serial input
                    while (true)
                    {
                        while (dataReaderObject.UnconsumedBufferLength != 0)
                        {
                            await Task.Delay(200);
                            Debug.WriteLine(dataReaderObject.UnconsumedBufferLength);
                        }
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {

                
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    Debug.WriteLine("Listen: Reading task was cancelled, closing device and cleaning up");
                }
                else
                {
                   // Debug.WriteLine("Listen: " + ex.Message);
                }
            }
            finally
            {
                // Očisti kada je završeno
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }


        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 5;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.ReadAhead;


            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);


            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;



            string[] value;
            char side;

            if (bytesRead > 0)
            {
                try
                {
                    string recvdtxt = dataReaderObject.ReadString(bytesRead);
                    Debug.WriteLine(recvdtxt);

                    value = Regex.Split(recvdtxt, @"\D+");
                    side = recvdtxt[0];
                    await playSound(side, value[1]);


                }
                catch (Exception ex)
                {
                   // Debug.WriteLine("ReadAsync: " + ex.Message);
                }

            }


        }

        private async Task playSound(char side, string v)
        {
            if (C1_beep.CurrentState == MediaElementState.Playing || 
                C2_beep.CurrentState == MediaElementState.Playing ||
                C3_beep.CurrentState == MediaElementState.Playing || 
                R1_beep.CurrentState == MediaElementState.Playing ||
                R2_beep.CurrentState == MediaElementState.Playing || 
                R3_beep.CurrentState == MediaElementState.Playing || 
                L1_beep.CurrentState == MediaElementState.Playing || 
                L2_beep.CurrentState == MediaElementState.Playing || 
                L3_beep.CurrentState == MediaElementState.Playing ||
                RC1_beep.CurrentState == MediaElementState.Playing ||
                RC2_beep.CurrentState == MediaElementState.Playing || 
                RC3_beep.CurrentState == MediaElementState.Playing ||
                LC1_beep.CurrentState == MediaElementState.Playing ||
                LC2_beep.CurrentState == MediaElementState.Playing ||
                LC3_beep.CurrentState == MediaElementState.Playing ||
                right_intro.CurrentState == MediaElementState.Playing||
                left_intro.CurrentState == MediaElementState.Playing)
                   return;
                if (side == 'l')
            {
                if (Convert.ToInt16(v) <= 70)
                    L1_beep.Play();
                else if (Convert.ToInt16(v) > 70 && Convert.ToInt16(v) <= 120)
                    L2_beep.Play();
                else
                    L3_beep.Play();
            }
            else if (side == 'c')
            {
                if (Convert.ToInt16(v) <= 70)
                    C1_beep.Play();
                else if (Convert.ToInt16(v) > 70 && Convert.ToInt16(v) <= 120)
                    C2_beep.Play();
                else
                    C3_beep.Play();
            }
            else if (side == 'r')
            {
                if (Convert.ToInt16(v) <= 70)
                    R1_beep.Play();
                else if (Convert.ToInt16(v) > 70 && Convert.ToInt16(v) <= 120)
                    R2_beep.Play();
                else
                    R3_beep.Play();
            }
            else if(side == 'a')
            {
                if (Convert.ToInt16(v) <= 70)
                    LC1_beep.Play();
                else if (Convert.ToInt16(v) > 70 && Convert.ToInt16(v) <= 120)
                    LC2_beep.Play();
                else
                    LC3_beep.Play();
            }
            else if(side== 'b')
            {
                if (Convert.ToInt16(v) <= 70)
                    RC1_beep.Play();
                else if (Convert.ToInt16(v) > 70 && Convert.ToInt16(v) <= 120)
                    RC2_beep.Play();
                else
                    RC3_beep.Play();
            }

        }
        

        //ako zatreba cancel za primanje
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }


    }
}
