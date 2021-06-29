using System;
using System.ComponentModel;
using VideoOS.Platform.Proxy.Alarm;

namespace AlarmList
{
    public enum AlarmStates
    {
        New = 1,
        InProgress = 4,
        OnHold = 9,
        Closed = 11,
    }

    public class ObservableAlarmLine : INotifyPropertyChanged
    {

        /// <summary>
        /// Constructor for use when alarm data exists at creation time
        /// </summary>
        public ObservableAlarmLine(AlarmLine alarmLine)
		{
			AlarmLine = alarmLine;
		}

        /// <summary>
        /// Gets or sets the AlarmLine data embedded in the ObservableAlarm
        /// </summary>
        public AlarmLine AlarmLine { get; set; } = null;

        /// <summary>
        /// Gets the identifier as a number - human readable id
        /// </summary>
        public int LocalId
        {
            get
            {
                if (AlarmLine != null)
                {
                    return AlarmLine.LocalId;
                }
                else
                {
                    return -1;
                }
            }
        }
        
        /// <summary>
        /// Gets the state as a number
        /// </summary>
        public int State
        {
            get
            {
                if (AlarmLine != null)
                {
                    return AlarmLine.State;
                }
                else
                {
                    return -1;
                }
            }
            set
            {
                if (AlarmLine != null && AlarmLine.State != (ushort)value)
                {
                    AlarmLine.State = (ushort)value;
                    NotifyPropertyChanged("StateString");
                }
            }
        }
        
        /// <summary>
        /// Gets the state as a string
        /// </summary>
        public string StateString
        {
            get
            {
                if (AlarmLine != null)
                {
                    AlarmStates s = (AlarmStates)AlarmLine.State;
                    switch (s)
                    {
                        case AlarmStates.Closed:
                            return "Closed";
                        case AlarmStates.InProgress:
                            return "In progress";
                        case AlarmStates.New:
                            return "New";
                        case AlarmStates.OnHold:
                            return "On hold";
                        default:
                            return AlarmLine.State.ToString(); // States may have arbitrary values stored if some vendor wants to use that
                    }
                }
                else
                {
                    return "";
                }
            }
        }

        /// <summary>
        /// Gets the priority as a number
        /// </summary>
        public int Priority
        {
            get
            {
                if (AlarmLine != null)
                {
                    return AlarmLine.Priority;
                }
                else
                {
                    return -1;
                }
            }
            set
            {
                if (AlarmLine != null && AlarmLine.Priority != (ushort)value)
                {
                    AlarmLine.Priority = (ushort)value;
                }
            }
        }

        /// <summary>
        /// Gets the message
        /// </summary>
        public string Message
        {
            get
            {
                if (AlarmLine != null && AlarmLine.Message != null)
                {
                    return AlarmLine.Message.Trim();
                }
                else
                {
                    return "";
                }
            }
        }

        /// <summary>
        /// Gets the timestamp as a string
        /// </summary>
        public string Timestamp
        {
            get
            {
                if (AlarmLine != null && AlarmLine.Timestamp != null)
                {
                    DateTime tim = AlarmLine.Timestamp.ToLocalTime();
                    return tim.ToLongTimeString() + " " + tim.ToShortDateString();
                }
                else
                {
                    return "";
                }
            }
        }

        #region Events and Protected Methods

        /// <summary>
        /// Needed for coordination with WPF controls
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Needed for coordination with WPF controls
        /// </summary>
        /// <param name="propertyName"></param>
        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}
