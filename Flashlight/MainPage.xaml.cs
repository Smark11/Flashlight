using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Flashlight.Resources;
using Windows.Phone.Devices.Power;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Phone.Media.Capture;
using System.Diagnostics;
using Common.IsolatedStoreage;
using Microsoft.Phone.Tasks;

namespace Flashlight
{
    public enum Screen
    {
        Main,
        Settings,
        About
    }

    public partial class MainPage : PhoneApplicationPage, INotifyPropertyChanged, IDisposable
    {
        public static MainPage _mainPageInstance;

        private object _lockObject = new object();

        private const string ONSTARTUPCHECKED = "onstartupchecked";
        private const string HOLDONCHECKED = "holdonchecked";
        private const string STROBEFREQUENCY = "strobefrequency";
        private const string STROBEON = "strobeon";
        private const string ONWHENLOCKED = "onwhenlocked";

        MarketplaceDetailTask _marketPlaceDetailTask = new MarketplaceDetailTask();
        MarketplaceReviewTask _marketPlaceReviewTask = new MarketplaceReviewTask();

        // Constructor
        public MainPage()
        {
            _mainPageInstance = this;

            SetScreenWidth();

            InitializeComponent();

            StrobeFrequency = 1000;

            LoadSettings();

            PictureSource = "/OffButton.png";

            //Start going to Main Screen
            GoToScreen(Screen.Main);

            DataContext = this;

            StartMonitoringBattery();

            InitializeFlash();
        }

        private void SetScreenWidth()
        {
            ScreenWidth = (int)Application.Current.Host.Content.ActualWidth;
        }

        public void UpdateFlipTile(
            string title,
            string backTitle,
            string backContent,
            string wideBackContent,
            int count,
            Uri tileId,
            Uri smallBackgroundImage,
            Uri backgroundImage,
            Uri backBackgroundImage,
            Uri wideBackgroundImage,
            Uri wideBackBackgroundImage)
        {

            // Get the new FlipTileData type.
            Type flipTileDataType = Type.GetType("Microsoft.Phone.Shell.FlipTileData, Microsoft.Phone");

            // Get the ShellTile type so we can call the new version of "Update" that takes the new Tile templates.
            Type shellTileType = Type.GetType("Microsoft.Phone.Shell.ShellTile, Microsoft.Phone");

            if (ShellTile.ActiveTiles != null)
            {
                // Loop through any existing Tiles that are pinned to Start.
                foreach (var tileToUpdate in ShellTile.ActiveTiles)
                {
                    // Look for a match based on the Tile's NavigationUri (tileId).
                    //if (tileToUpdate.NavigationUri.ToString() == tileId.ToString())
                    //{
                    // Get the constructor for the new FlipTileData class and assign it to our variable to hold the Tile properties.
                    var UpdateTileData = flipTileDataType.GetConstructor(new Type[] { }).Invoke(null);

                    // Set the properties. 
                    SetProperty(UpdateTileData, "Title", title);
                    SetProperty(UpdateTileData, "Count", count);
                    SetProperty(UpdateTileData, "BackTitle", backTitle);
                    SetProperty(UpdateTileData, "BackContent", backContent);
                    SetProperty(UpdateTileData, "SmallBackgroundImage", smallBackgroundImage);
                    SetProperty(UpdateTileData, "BackgroundImage", backgroundImage);
                    SetProperty(UpdateTileData, "BackBackgroundImage", backBackgroundImage);
                    SetProperty(UpdateTileData, "WideBackgroundImage", wideBackgroundImage);
                    SetProperty(UpdateTileData, "WideBackBackgroundImage", wideBackBackgroundImage);
                    SetProperty(UpdateTileData, "WideBackContent", wideBackContent);

                    // Invoke the new version of ShellTile.Update.
                    shellTileType.GetMethod("Update").Invoke(tileToUpdate, new Object[] { UpdateTileData });
                    break;
                    //}

                }
            }

        }

        private static void SetProperty(object instance, string name, object value)
        {
            var setMethod = instance.GetType().GetProperty(name).GetSetMethod();
            setMethod.Invoke(instance, new object[] { value });
        }

        #region settings

        private void SaveSettings()
        {
            IS.SaveSetting(ONSTARTUPCHECKED, OnStartUpChecked);
            IS.SaveSetting(HOLDONCHECKED, HoldOnChecked);
            IS.SaveSetting(STROBEFREQUENCY, StrobeFrequency);
            IS.SaveSetting(STROBEON, StrobeOn);
            IS.SaveSetting(ONWHENLOCKED, OnWhenLockedChecked);
        }

        public void LoadSettings()
        {
            if (IS.GetSetting(ONSTARTUPCHECKED) != null)
            {
                OnStartUpChecked = (bool)IS.GetSetting(ONSTARTUPCHECKED);
            }

            if (IS.GetSetting(HOLDONCHECKED) != null)
            {
                HoldOnChecked = (bool)IS.GetSetting(HOLDONCHECKED);
            }

            if (IS.GetSetting(STROBEFREQUENCY) != null)
            {
                StrobeFrequency = (int)IS.GetSetting(STROBEFREQUENCY);
            }
            else
            {
                StrobeFrequency = 1000;
            }


            if (IS.GetSetting(STROBEON) != null)
            {
                StrobeOn = (bool)IS.GetSetting(STROBEON);
            }
            else
            {
                StrobeOn = false;
            }

            if (IS.GetSetting(ONWHENLOCKED) != null)
            {
                OnWhenLockedChecked = (bool)IS.GetSetting(ONWHENLOCKED);
            }
            else
            {
                OnWhenLockedChecked = false;
            }
        }

        public void ToggleAutoOn()
        {
            if (OnStartUpChecked)
            {
                _lightOn = true;
                TurnFlashOn();
            }
        }

        #endregion settings

        public void ToggleLightWhenScreenLocked()
        {
            if (!OnWhenLockedChecked)
            {
                TurnFlashOff();
            }
        }

        private void GoToScreen(Screen screen)
        {
            switch (screen)
            {
                case Screen.Main:
                    {
                        MainScreenVisibility = System.Windows.Visibility.Visible;
                        SettingsVisibility = System.Windows.Visibility.Collapsed;
                        AboutScreenVisibility = System.Windows.Visibility.Collapsed;
                    }
                    break;
                case Screen.Settings:
                    {
                        MainScreenVisibility = System.Windows.Visibility.Collapsed;
                        SettingsVisibility = System.Windows.Visibility.Visible;
                        AboutScreenVisibility = System.Windows.Visibility.Collapsed;
                    }
                    break;
                case Screen.About:
                    {
                        MainScreenVisibility = System.Windows.Visibility.Collapsed;
                        SettingsVisibility = System.Windows.Visibility.Collapsed;
                        AboutScreenVisibility = System.Windows.Visibility.Visible;
                        break;
                    }
            }


        }

        #region flash

        AudioVideoCaptureDevice _avDevice;

        private async void SetupFlash()
        {
            try
            {
                ButtonVisibility = Visibility.Collapsed;
                InitializeVisibility = Visibility.Visible;

                // get the AudioViceoCaptureDevice
                _avDevice = await AudioVideoCaptureDevice.OpenAsync(CameraSensorLocation.Back,
                    AudioVideoCaptureDevice.GetAvailableCaptureResolutions(CameraSensorLocation.Back).First());

                ButtonVisibility = Visibility.Visible;
                InitializeVisibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {

            }
        }

        private async void InitializeFlash()
        {
            try
            {
                SetupFlash();

                ToggleAutoOn();

            }
            catch (Exception ex)
            {
                // Flashlight isn't supported on this device, instead show a White Screen as the flash light
                //ShowWhiteScreenInsteadOfCameraTorch();
            }

        }

        private async void TurnFlashOn()
        {
            if (_avDevice == null)
            {
                SetupFlash();
            }

            try
            {
                _avDevice.SetProperty(KnownCameraAudioVideoProperties.VideoTorchMode, VideoTorchMode.On);

                _lightOn = true;
                PictureSource = "/onresized.png";
            }
            catch (Exception ex)
            {

            }
        }

        private void TurnFlashOff()
        {
            if (_avDevice != null)
            {
                try
                {
                    _avDevice.SetProperty(KnownCameraAudioVideoProperties.VideoTorchMode, VideoTorchMode.Off);

                    _lightOn = false;
                    PictureSource = "/OffButton.png";
                }
                catch (Exception ex)
                {

                }
            }
        }

        public void StartStrobe()
        {
            Task.Factory.StartNew(() => StartStrobeAsync());
        }

        public void StartStrobeAsync()
        {
            while (_strobeLightIsOn && _lightOn)
            {
                _avDevice.SetProperty(KnownCameraAudioVideoProperties.VideoTorchMode, VideoTorchMode.On);

                System.Threading.Thread.Sleep(1000 - StrobeFrequency);

                _avDevice.SetProperty(KnownCameraAudioVideoProperties.VideoTorchMode, VideoTorchMode.Off);
            }
        }

        #endregion flash

        #region batterypercentage

        private void StartMonitoringBattery()
        {
            Task.Factory.StartNew(StartMonitoringBatteryAsync);
        }

        private void StartMonitoringBatteryAsync()
        {
            while (_keepPollingBattery)
            {
                GetBatteryPercentage();

                System.Threading.Thread.Sleep(System.TimeSpan.FromSeconds(30));
            }
        }

        private void GetBatteryPercentage()
        {
            Dispatcher.BeginInvoke(() =>
                {
                    BatteryPercentage = Battery.GetDefault().RemainingChargePercent;
                    TimeSpan dischargeTime = Battery.GetDefault().RemainingDischargeTime;
                    TimeRemaining = dischargeTime.Hours + "Hours, " + dischargeTime.Minutes + "Minutes, " + dischargeTime.Seconds + "Seconds";
                }
            );

        }

        #endregion batterypercentage

        #region properties

        private bool _onWhenLockedChecked;
        public bool OnWhenLockedChecked
        {
            get { return _onWhenLockedChecked; }
            set { _onWhenLockedChecked = value; RaisePropertyChanged("OnWhenLockedChecked"); }
        }


        private int _strobeFrequency;
        public int StrobeFrequency
        {
            get { return _strobeFrequency; }
            set
            {
                _strobeFrequency = value;
                RaisePropertyChanged("StrobeFrequency");
            }
        }

        private bool _strobeOn;
        public bool StrobeOn
        {
            get { return _strobeOn; }
            set
            {
                _strobeOn = value;
                RaisePropertyChanged("StrobeOn");
            }
        }



        private bool _onStartUpChecked;
        public bool OnStartUpChecked
        {
            get { return _onStartUpChecked; }
            set
            {
                _onStartUpChecked = value;
                RaisePropertyChanged("OnStartUpChecked");
                OnStartUpText = (value) ? (OnStartUpText = "Auto On At Startup") : (OnStartUpText = "Off At Startup");
            }
        }

        private string _onStartupText;
        public string OnStartUpText
        {
            get { return _onStartupText; }
            set { _onStartupText = value; RaisePropertyChanged("OnStartUpText"); }
        }

        private bool _holdOnChecked;
        public bool HoldOnChecked
        {
            get { return _holdOnChecked; }
            set
            {
                _holdOnChecked = value;
                RaisePropertyChanged("HoldOnChecked");
                HoldOnText = (value) ? (HoldOnText = "Hold Button To Stay On") : (HoldOnText = "Hold Button Off");
            }
        }

        private string _holdOnText;
        public string HoldOnText
        {
            get { return _holdOnText; }
            set { _holdOnText = value; RaisePropertyChanged("HoldOnText"); }
        }


        private string _pictureSource;
        public string PictureSource
        {
            get { return _pictureSource; }
            set { _pictureSource = value; RaisePropertyChanged("PictureSource"); }
        }

        private bool _settingsScreenClicked = false;
        private bool _aboutScreenClicked = false;
        private bool _keepPollingBattery = true;

        private int _batteryPercentage;
        public int BatteryPercentage
        {
            get { return _batteryPercentage; }
            set { _batteryPercentage = value; RaisePropertyChanged("BatteryPercentage"); }
        }

        private string _timeRemaining;
        public string TimeRemaining
        {
            get { return _timeRemaining; }
            set { _timeRemaining = value; RaisePropertyChanged("TimeRemaining"); }
        }

        private Visibility _mainScreenVisibility;
        public Visibility MainScreenVisibility
        {
            get { return _mainScreenVisibility; }
            set { _mainScreenVisibility = value; RaisePropertyChanged("MainScreenVisibility"); }
        }

        private Visibility _settingsVisibility;
        public Visibility SettingsVisibility
        {
            get { return _settingsVisibility; }
            set { _settingsVisibility = value; RaisePropertyChanged("SettingsVisibility"); }
        }

        private Visibility _aboutScreenVisibility;
        public Visibility AboutScreenVisibility
        {
            get { return _aboutScreenVisibility; }
            set { _aboutScreenVisibility = value; RaisePropertyChanged("AboutScreenVisibility"); }
        }

        private int _screenWidth;
        public int ScreenWidth
        {
            get { return _screenWidth; }
            set { _screenWidth = value; RaisePropertyChanged("ScreenWidth"); }
        }

        private Visibility _buttonVisibility;
        public Visibility ButtonVisibility
        {
            get { return _buttonVisibility; }
            set { _buttonVisibility = value; RaisePropertyChanged("ButtonVisibility"); }
        }

        private Visibility _initializeVisibility;
        public Visibility InitializeVisibility
        {
            get { return _initializeVisibility; }
            set { _initializeVisibility = value; RaisePropertyChanged("InitializeVisibility"); }
        }

        #endregion properties

        #region propertychanged

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string prop)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }

        #endregion propertychanged

        #region dispose

        public void Dispose()
        {
            _keepPollingBattery = false;
            if (_avDevice != null)
            {
                _avDevice.SetProperty(KnownCameraAudioVideoProperties.VideoTorchMode, VideoTorchMode.Off);
            }

            SaveSettings();
        }

        #endregion dispose

        #region clickhandlers

        private void SettingsClicked(object sender, EventArgs e)
        {
            _settingsScreenClicked = true;

            GoToScreen(Screen.Settings);
        }

        private void BackButtonClicked(object sender, CancelEventArgs e)
        {
            if (_settingsScreenClicked || _aboutScreenClicked)
            {
                GoToScreen(Screen.Main);
                _settingsScreenClicked = false;
                _aboutScreenClicked = false;
                e.Cancel = true;
            }

            SaveSettings();
        }

        private void ManipulationCompleted(object sender, System.Windows.Input.ManipulationCompletedEventArgs e)
        {
            if (HoldOnChecked)
            {
                TurnFlashOff();
                _strobeLightIsOn = false;
                _lightOn = false;
            }
        }

        private bool _lightOn = false;
        private bool _strobeLightIsOn = false;

        private void ManipulationStarted(object sender, System.Windows.Input.ManipulationStartedEventArgs e)
        {
            if (StrobeOn)
            {
                StrobeModeToggle();
            }
            else
            {
                FlashLightOnlyModeToggle();
            }
        }

        private void StrobeModeToggle()
        {
            //strobing is turned off, turn it on
            if (!_strobeLightIsOn)
            {
                _strobeLightIsOn = true;
                _lightOn = true;
                TurnFlashOn();
                StartStrobe();
            }
            else
            {
                TurnFlashOff();
                _strobeLightIsOn = false;
                _lightOn = false;
            }
        }

        private void FlashLightOnlyModeToggle()
        {
            if (!_lightOn)
            {
                TurnFlashOn();
            }
            else
            {
                TurnFlashOff();
            }
        }

        private void GoToSpyCam(object sender, RoutedEventArgs e)
        {
            _marketPlaceDetailTask.ContentIdentifier = "9f817a42-0d6e-472c-b44a-f09f3a829ca4";
            _marketPlaceDetailTask.Show();
        }

        private void AboutClicked(object sender, EventArgs e)
        {
            _aboutScreenClicked = true;
            GoToScreen(Screen.About);
        }

        #endregion clickhandlers

        private void RatingsClicked(object sender, EventArgs e)
        {
            _marketPlaceReviewTask.Show();
        }
    }
}