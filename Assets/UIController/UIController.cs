﻿/*
UIController.cs is part of the Experica.
Copyright (c) 2016 Li Alex Zhang and Contributors

Permission is hereby granted, free of charge, to any person obtaining a 
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the 
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included 
in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF 
OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;
using System.Diagnostics;
using System.Threading;
using System.Runtime;
using System;
using System.IO;

namespace Experica.Environment
{
    public class UIController : MonoBehaviour
    {
        public InputField serveraddress;
        public Toggle clientconnect, autoconn;
        public Text autoconntext, version;
        public NetManager netmanager;
        public Canvas canvas;
        public EnvironmentConfig config;
        public PostProcessVolume postprocessvolume;
        readonly string configpath = "EnvironmentConfig.yaml";

        bool isautoconn, isconnect;
        int autoconncountdown;
        float lastautoconntime;
        int lastwindowwidth=800, lastwindowheight=600;

        /// <summary>
        /// Because the unorderly manner unity Awake monobehaviors, we need to set ApplicationManager
        /// as the first to Awake in unity project setting(Script Order), so that application wide 
        /// configuration will be ready for all other objects to use.
        /// </summary>
        void Awake()
        {
            Application.runInBackground = true;
            if (File.Exists(configpath))
            {
                config = configpath.ReadYamlFile<EnvironmentConfig>();
            }
            if (config == null)
            {
                config = new EnvironmentConfig();
            }
        }

        public float GetAspectRatio()
        {
            var rootrt = canvas.gameObject.transform as RectTransform;
           // return rootrt.rect.width / rootrt.rect.height;

            return Screen.width.Convert<float>() / Screen.height;
        }

        public void OnRectTransformDimensionsChange()
        {
            if (isconnect)
            {
                netmanager.client.Send(MsgType.AspectRatio, new FloatMessage(GetAspectRatio()));
            }
        }

        public void OnToggleClientConnect(bool isconn)
        {
            if (isconn)
            {
                netmanager.networkAddress = serveraddress.text;
                netmanager.StartClient();
            }
            else
            {
                netmanager.StopClient();
                OnClientDisconnect();
            }
        }

        public void OnServerAddressEndEdit(string v)
        {
            config.ServerAddress = v;
        }

        public void OnToggleAutoConnect(bool ison)
        {
            config.AutoConnect = ison;
            ResetAutoConnect();
        }

        public void ResetAutoConnect()
        {
            autoconncountdown = config.AutoConnectTimeOut;
            isautoconn = config.AutoConnect;
            if (!isautoconn)
            {
                autoconntext.text = "Auto Connect OFF";
            }
            autoconn.isOn = isautoconn;
        }

        void Start()
        {
            version.text = $"Version {Application.version}\nUnity {Application.unityVersion}";
            serveraddress.text = config.ServerAddress;
            ResetAutoConnect();
        }

        public void SetCLUT(Texture2D lut)
        {
            ColorGrading colorgrading;
            if (postprocessvolume.profile.TryGetSettings(out colorgrading))
            {
                colorgrading.ldrLut.value = lut;
            }
        }

        public void ToggleColorGrading(bool ison)
        {
            ColorGrading colorgrading;
            if (postprocessvolume.profile.TryGetSettings(out colorgrading))
            {
                colorgrading.enabled.value = ison;
            }
        }

        void Update()
        {
            if (!isconnect)
            {
                if (Input.GetButton("Quit"))
                {
                    Application.Quit();
                    return;
                }
                if (Input.GetButtonDown("FullScreen"))
                {
                    if (Screen.fullScreen)
                    {
                        Screen.SetResolution(lastwindowwidth, lastwindowheight, false);
                    }
                    else
                    {
                        lastwindowwidth = Math.Max(800, Screen.width);
                        lastwindowheight =Math.Max(600, Screen.height);
                        Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                    }
                }
                if (isautoconn)
                {
                    if (Time.unscaledTime - lastautoconntime >= 1)
                    {
                        autoconncountdown--;
                        if (autoconncountdown > 0)
                        {
                            lastautoconntime = Time.unscaledTime;
                            autoconntext.text = "Auto Connect " + autoconncountdown + "s";
                        }
                        else
                        {
                            clientconnect.isOn = true;
                            clientconnect.onValueChanged.Invoke(true);
                            autoconntext.text = "Connecting ...";
                            isautoconn = false;
                        }
                    }
                }
            }
        }

        public void OnClientConnect()
        {
            isconnect = true;
            autoconntext.text = "Connected";
            // since VLabEnvironment is to provide virtual reality environment, we may want to
            // hide cursor and ui when connected to VLab.
            canvas.enabled = !config.HideUIWhenConnected;
            Cursor.visible = !config.HideCursorWhenConnected;
            // when connected to VLab, we need to make sure that all system resourses
            // VLabEnvironment needed is ready to start experiment.
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = config.VSyncCount;
            QualitySettings.maxQueuedFrames = config.MaxQueuedFrames;
            Time.fixedDeltaTime = config.FixedDeltaTime;

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
            Process.GetCurrentProcess().PriorityBoostEnabled = true;
            GC.Collect();
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;
            //if (!GC.TryStartNoGCRegion(1000000000))
            //{
            //    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            //}
        }

        public void OnClientDisconnect()
        {
            isconnect = false;
            // when disconnected, we should go back to ui and turn on cursor.
            ResetAutoConnect();

            var callback = clientconnect.onValueChanged;
            clientconnect.onValueChanged = new Toggle.ToggleEvent();
            clientconnect.isOn = false;
            clientconnect.onValueChanged = callback;

            canvas.enabled = true;
            Cursor.visible = true;
            // when disconnect, we can relax and release some system resourses for other process
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.vSyncCount = 1;
            QualitySettings.maxQueuedFrames = 1;
            Time.fixedDeltaTime = 0.02f;

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Normal;
            //if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            //{
            //    GC.EndNoGCRegion();
            //}
            //else
            //{
            //    GCSettings.LatencyMode = GCLatencyMode.Interactive;
            //}
            GCSettings.LatencyMode = GCLatencyMode.Interactive;
            GC.Collect();
        }

        void OnApplicationQuit()
        {
            if (netmanager.IsClientConnected())
            {
                netmanager.StopClient();
            }
            configpath.WriteYamlFile(config);
        }

    }
}