﻿using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using RestSharp;
using Syncfusion.Maui.Scheduler;
using System.Collections.ObjectModel;
using System.Diagnostics;
using WorkScheduler.Models;
using WorkScheduler.Views;

namespace WorkScheduler.ViewModels
{
    internal partial class SchedulerViewModel : ObservableObject
    {
        private string _accessToken = "czWjvBwr4eWYX32ZZsJGhw==";
        private RestClient _client = new RestClient("http://localhost:5000");
        private IList<DateTime> _visibleDates;
        private SchedulerAppointment _selectedAppointment;

        public SchedulerViewModel()
        {
            TappedCommand = new Command<SchedulerTappedEventArgs>(OnSchedulerTapped);
            OnViewChangedCommand = new Command<SchedulerViewChangedEventArgs>(OnVeiwChangedAsync);
            //_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            SchedulerEvents.Add(new SchedulerAppointment
            {
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                Subject = "test"
            });
        }

        private Command<SchedulerTappedEventArgs> _tappedCommand;

        public Command<SchedulerTappedEventArgs> TappedCommand
        {
            get { return _tappedCommand; }
            set { _tappedCommand = value; }
        }

        private Command<SchedulerViewChangedEventArgs> _onViewChangedCommand;

        public Command<SchedulerViewChangedEventArgs> OnViewChangedCommand
        {
            get { return _onViewChangedCommand; }
            set { _onViewChangedCommand = value; }
        }

        public ObservableCollection<SchedulerAppointment> SchedulerEvents { get; set; } = new ObservableCollection<SchedulerAppointment>();

        private void OnSchedulerTapped(SchedulerTappedEventArgs e)
        {
            if (e is not null)
            {
                _selectedAppointment = (SchedulerAppointment)e.Appointments.First();
            }
            else
            {
                _selectedAppointment = null;
            }
        }

        private async void OnVeiwChangedAsync(SchedulerViewChangedEventArgs e)
        {
            if (e is not null)
            {
                _visibleDates = e.NewVisibleDates;
                await UpdateScheduler(e.NewVisibleDates);
            }
        }

        private async Task UpdateScheduler(IList<DateTime> targetDates)
        {
            SchedulerEvents.Clear();

            foreach (var targetDate in targetDates)
            {
                var request = new RestRequest($"/api/workschedule/{targetDate.Year}/{targetDate.Month}/{targetDate.Day}");
                request.AddHeader("AccessToken", _accessToken);

                try
                {
                    // 予定が0件の場合もExceptionが発生するため。
                    var response = await _client.GetAsync(request);
                    // 予定が1件の場合非配列JSON、予定が複数件の場合配列型JSON
                    if (response.Content[0] == '[')
                    {
                        var schedules = JsonConvert.DeserializeObject<List<Schedule>>(response.Content);
                        foreach (var schedule in schedules)
                        {
                            SchedulerEvents.Add(new SchedulerAppointment
                            {
                                StartTime = schedule.StartTime,
                                EndTime = schedule.EndTime,
                                Subject = CreateSubject(schedule.WorkStyle, schedule.WorkingPlace),
                            });
                        }
                    }
                    else
                    {
                        var schedule = JsonConvert.DeserializeObject<Schedule>(response.Content);
                        SchedulerEvents.Add(new SchedulerAppointment
                        {
                            StartTime = schedule.StartTime,
                            EndTime = schedule.EndTime,
                            Subject = CreateSubject(schedule.WorkStyle, schedule.WorkingPlace),
                        });
                    }

                }
                catch (Exception ex)
                {
                    // Do nothing
                    Debug.WriteLine(ex);
                }
            }
        }

        [RelayCommand]
        private async void ShowInputDetails()
        {
            var result = await Shell.Current.ShowPopupAsync(new InputDetails());
            if (result is InputDetailsContact output)
            {
                var eventInfo = new EventInfo
                {
                    Date = output.Date.ToString("yyyy-MM-dd"),
                    StartTime = output.Date.ToString("yyyy-MM-dd") + "T" + output.StartTime.ToString(@"hh\:mm"),        // Format:"2023-01-30T08:40"
                    EndTime = output.Date.ToString("yyyy-MM-dd") + "T" + output.EndTime.ToString(@"hh\:mm"),
                    WorkStyle = output.WorkStyle,
                    WorkingPlace = output.WorkingPlace,
                };

                var request = new RestRequest($"/api/workschedule/{output.Date.Year}/{output.Date.Month}/{output.Date.Day}");
                request.AddHeader("AccessToken", _accessToken);
                request.AddBody(eventInfo);
                try
                {
                    var response = await _client.PostAsync(request);
                    await UpdateScheduler(_visibleDates);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }


        [RelayCommand]
        private void EditSchedule()
        {

        }


        [RelayCommand]
        private async void DeleteSchedule()
        {
            if (_selectedAppointment is not null)
            {
                var request = new RestRequest($"/api/workschedule/{_selectedAppointment.StartTime.Year}/{_selectedAppointment.StartTime.Month}/{_selectedAppointment.StartTime.Day}");
                request.AddHeader("AccessToken", _accessToken);
                await _client.DeleteAsync(request);
                await UpdateScheduler(_visibleDates);
            }
        }

        private string CreateSubject(string workStyle, string workingPlace)
        {
            if (string.IsNullOrEmpty(workingPlace))
            {
                return $"{workStyle}";
            }
            return $"{workStyle}[{workingPlace}]";
        }
    }

    internal class EventInfo
    {
        public string Date { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string WorkStyle { get; set; }
        public string WorkingPlace { get; set; }
    }

}
