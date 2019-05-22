﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections.Concurrent;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Threading;

namespace Tracker
{


    public sealed class Tracker
    {
   
        private static Tracker _instance;
        private WebSocket _socket;
        private ConcurrentQueue<TrackerEvent> _eventQueue;
        const int MAX_ELEMS = 500;
        //path where it will be saved
        private string _path;
        //list of all serializers
        private List<Serializer> _serializerList;

        //Thread
        private Thread thread;
        private bool _running = false;


        ///time which the program must wait between piece data deliveries
        private float _deliveryTime;


        /// <summary>
        /// Struct for controlling the Tracker delivery rate in order to optimize device battery
        /// FULL: Increase the data delivery frequency to the maximum
        /// HIGH: Increase the data delivery frequency to a high level
        /// MODERATE: Increase the data delivery frequency to moderate level
        /// LOW: Makes a minor increment in the data delivery frequency
        /// NONE: The frequency of delivery isn't affected
        /// </summary>
        enum Optimize { FULL, HIGH, MODERATE, LOW, NONE };

        private Optimize CheckBatteryStatus()
        {
            float batteryLevel = SystemInfo.batteryLevel;
            BatteryStatus batteryStatus = SystemInfo.batteryStatus;

            if (batteryLevel == -1)
            {
#if DEBUG
                Debug.Log("Current platform doesn´t support battery level info");

#endif
                return Optimize.NONE;
            }
            if (batteryStatus == BatteryStatus.Discharging || batteryStatus == BatteryStatus.NotCharging)
            {
                if (batteryLevel >= 0.75f)// LOW
                {
                    return Optimize.LOW;
                }
                else if (batteryLevel >= 0.5f)// MODERATE
                {
                    return Optimize.MODERATE;
                }
                else if (batteryLevel >= 0.3f)// HIGH
                {
                    return Optimize.HIGH;
                }
                else if (batteryLevel < 0.3f)// FULL
                {
                    return Optimize.FULL;
                }
                else
                    return Optimize.NONE;// NONE
            }
            else
            {
                return Optimize.NONE;// NONE
            }
        }

        // DESPUES DEL GUARDADO DE DATOS CHEQUEAR EL NIVEL DE OPTIMIZACÍON REQUERIDO
        private void OptimizeTracker(Optimize level)
        {
            switch (level)
            {
                case Optimize.FULL:
                    _deliveryTime = 90.0f;
                    break;
                case Optimize.HIGH:
                    _deliveryTime = 60.0f;
                    break;
                case Optimize.MODERATE:
                    _deliveryTime = 30.0f;
                    break;
                case Optimize.LOW:
                    _deliveryTime = 15.0f;
                    break;
                case Optimize.NONE:
                    _deliveryTime = 7.0f;
                    break;
            }
        }

        //Types of connectivity
        enum Connectivity { NOINTERNET, CARRIERDATA, WIFI}
        //Check device connection
        private Connectivity ConnectivityStatus()
        {
            //Not reachable
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return Connectivity.NOINTERNET;
            }
            //Check if the device can reach the internet via a carrier data network
            else if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
            {
                return Connectivity.CARRIERDATA;
            }
            //Check if the device can reach the internet via a LAN (WIFI...)
            else
            {
                return Connectivity.WIFI;
            }
        }


        //struct serializer with the serializer and his control bool
        struct Serializer
        {
            public SerializerInterface _serializer;
            public bool _active;
        }


        private Tracker()
        {
            _eventQueue = new ConcurrentQueue<TrackerEvent>();
            _serializerList = new List<Serializer>();
            _socket = new WebSocket("ws://localhost:4649/server");
            ConfigureSocket();
            thread = new Thread(ProcessData);
            //thread.Start();
            //_running = true;
        }

        public static Tracker Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Tracker();
                return _instance;
            }
        }

        //Set a path
        public void SetPath(string path)
        {
            _path = path;
        }

        //Add new serializers
        public void AddSerializer(SerializerInterface sI, bool active)
        {
            Serializer s;
            s._serializer = sI;
            s._active = active;
            _serializerList.Add(s);
        }

    
        //Add an event
        public void AddEvent(TrackerEvent e)
        {

            if (_eventQueue.Count < MAX_ELEMS)
            {
                _eventQueue.Enqueue(e);
            }
        }

        //Force add an event
        public void ForceAddEvent(TrackerEvent e)
        {

            if (_eventQueue.Count < MAX_ELEMS)
            {
                _eventQueue.Enqueue(e);
            }
            else
            {
                TrackerEvent aux;
                _eventQueue.TryDequeue(out aux);
                _eventQueue.Enqueue(e);
            }
        }


        public void ProcessData()//TODO: mirar el tema de guardar cuando no ay conexion y luego enviar lo guardado y comprir todo
        {
            float tIni = DateTime.Now.Second;
            float tEnd;
            while (_running) {
                tEnd = DateTime.Now.Second;
                OptimizeTracker(CheckBatteryStatus());
                if (Math.Abs(tEnd - tIni) >= _deliveryTime)
                {
                    if (ConnectivityStatus() == Connectivity.NOINTERNET) {
                        LocalDumpData();
                    }
                    else
                    {
                        /*if(mirar si hay algo guardado){
                            enviar lo guardado y lo nuevo
                        }
                        else{
                            solo lo nuevo
                        }*/
                        ServerDumpData();
                    }
                }
            }
        }

        //Proccess event queue
        public void LocalDumpData()
        { 
            while (_eventQueue.Count > 0)
            {
                foreach (var s in _serializerList)
                {
                    if (s._active)
                    {
                        s._serializer.DumpEvent(_eventQueue.First(), _path);
                    }
                }

                TrackerEvent aux;
                _eventQueue.TryDequeue(out aux);
            }
        }

        //Proccess event queue
        public void ServerDumpData()
        {
            throw new NotImplementedException();

        }



        private void ConfigureSocket()
        {
            _socket.OnOpen += (sender, e) => _socket.Send("Hi, there!");

            _socket.OnMessage += (sender, e) =>
               Debug.Log("OnMessage");
                
            _socket.OnError += (sender, e) =>
                Debug.Log("OnError");

            _socket.OnClose += (sender, e) =>
                 Debug.Log("OnClose");


        }

        //Send information to server
        public void Send()
        {

            //Crear archivo binario
            while (_eventQueue.Count > 0)
            {
               

                 _serializerPrueba._serializer.DumpEvent(_eventQueue.First(), _path);
                 TrackerEvent aux;
                 _eventQueue.TryDequeue(out aux);

            }
            FileInfo binaryFile = new FileInfo(_path + _serializerPrueba._serializer.GetFileName() + ".json");
            if (binaryFile.Exists)
            {
                _socket.Connect();
                byte[] b = new byte[1];
                b[0] = 125;
                _socket.Send(b);

                _socket.Close();
            }

        }
    }
}