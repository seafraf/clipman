﻿using LiteDB;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Clipple.AudioFilters
{
    public abstract class AudioFilter : ObservableObject
    {
        #region Methods
        public abstract UserControl GenerateControl();

        public abstract void Initialise();

        public virtual void CopyFrom<T>(T other) where T: AudioFilter
        {
            IsEnabled = other.IsEnabled;
        }
        #endregion

        #region Members
        private bool isEnabled;
        #endregion

        #region Properties
        [BsonIgnore]
        public abstract string FilterString { get; }

        [BsonIgnore]
        public abstract string FilterName { get; }

        public bool IsEnabled
        {
            get => isEnabled;
            set => SetProperty(ref isEnabled, value);
        }
        #endregion
    }
}
