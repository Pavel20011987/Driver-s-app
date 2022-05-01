using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Plugin.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;


namespace TestXamarin
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Page2 : ContentPage
    {
        private double startLatitude;
        private double endLatitude;
        private double startLongitude;
        private double endLongitude;

        private int step = STEP_NO;

        private const int STEP_NO = -1;
        private const int STEP_START_DAY = 0;
        private const int STEP_DINNER = 1;
        private const int STEP_CONTINUE_WORK = 2;
        private const int STEP_END_WORK = 3;

        private readonly HttpClient client = new HttpClient();

        private List<Car> cars = new List<Car>();
        private List<WorkingDay> workingDays = new List<WorkingDay>();

        private int workingDayId;

        private bool exitTimer = false;
       
        enum typeLocation { startLocation, endLocation}


        public static int userId;
        public static string userName;

        public static string login;
        public static string password;

        public static bool firstLaunch = true;
        

        public Page2()
        {
            
            InitializeComponent();

            //инициализация тулбара, кнопка выйти
            ToolbarItem toolbar = new ToolbarItem()
            {
                Text = "Выход",
                Order = ToolbarItemOrder.Primary,
                Priority = 0,
                

            };

            //установление действия на кнопку выйти
            toolbar.Clicked += async (s, e) =>
            {
                bool result = await DisplayAlert(null, "Вы уверены, что хотите выйти?", "ДА", "НЕТ");

                if (result)
                {
                    CrossSettings.Current.Remove("password");
                    Navigation.RemovePage(this);
                    await Navigation.PopAsync();
                    
                }
            };

            ToolbarItems.Add(toolbar);
        }

       

        //запуск при инициализации страницы
        protected override async void OnAppearing()
        {
            StateOfActivityFrame();
            //client.DefaultRequestHeaders.Accept.Clear();
            //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //при первом запуске страницы
            if (firstLaunch)
            {

                //устанавливаем логин и пароль на сервер
                var authData = string.Format("{0}:{1}", login, password);
                var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(authData));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);


                //получаем автомобили и записываем номера машин
                HttpResponseMessage response = await client.GetAsync(ServerUrl.carsUrl);
                string result = await response.Content.ReadAsStringAsync();

                cars = JsonConvert.DeserializeObject<List<Car>>(result);

                foreach (Car car in cars)
                {
                    numberPicker.Items.Add(car.number.ToString());
                }

                //получем(обновляем) актвных водителей, для получения процесса работы(шагов)
                await Update_WorkingDays();

                firstLaunch = false;
            }

            StateOfActivityFrame();

            base.OnAppearing();
        }

        
        //клик по кнопке начать(окончить) день
        private async void ButtonDay_Clicked(object sender, EventArgs e)
        {
            StateOfActivityFrame();

            if (dayButton.Text == "Начать день")
            {

                //логика начала дня

                //проверка поля пробег на начало дня на пустоту
                if (runStartEntry.Text == null)
                {
                    StateOfActivityFrame();
                    await DisplayAlert("Уведомление", "Пожалуйста, введите пробег на начало дня", "ОК");

                    return;
                }

                //если пробег автомобиля пуст, то отправляем его на сервер
                if (cars[numberPicker.SelectedIndex].mileage == 0)
                {
                    int startMileage = Convert.ToInt32(runStartEntry.Text);

                    StringContent stringContent = new StringContent(JsonConvert.SerializeObject(new
                    {
                        number = cars[numberPicker.SelectedIndex].number,
                        mileage = startMileage,
                    }));
                    stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    HttpResponseMessage message = await client.PutAsync(ServerUrl.carsUrl + cars[numberPicker.SelectedIndex].number + "/", stringContent);

                    cars[numberPicker.SelectedIndex].mileage = startMileage;
                }


                await GetCurrentLocation(typeLocation.startLocation);

                if (startLatitude == 0 || startLongitude == 0)
                {
                    StateOfActivityFrame();
                    await DisplayAlert("Уведомление", "Пожалуйста, включите геолокацию", "ОК");
                    
                    return; 
                }




                step = STEP_START_DAY;

                //записываем данные для отправки на сервер
                var content = new StringContent(JsonConvert.SerializeObject(new { 
                    driver = userId, 
                    car = cars[numberPicker.SelectedIndex].id,
                    geolocation_start = startLatitude + "|" + startLongitude,
                    step = step,
                    mileage_start = cars[numberPicker.SelectedIndex].mileage
                }));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PostAsync(ServerUrl.workdays, content);

                

                //string result = await response.Content.ReadAsStringAsync();

                //await DisplayAlert(null, result, "ok");


                //получем(обновляем) актвных водителей, для получения процесса работы(шагов) и получаем ид активного водителя
                await SetWorkingDayStatus();

                dayButton.Text = "Окончить день";

                dinnerButton.IsEnabled = true;
                dayButton.IsEnabled = false;
                runStartEntry.IsEnabled = false;

                //CheckFillElements();
            }

            else
            {
                //логика окончания дня

                await GetCurrentLocation(typeLocation.endLocation);

                if (endLatitude == 0 || endLongitude == 0)
                {
                    StateOfActivityFrame();
                    await DisplayAlert("Уведомление", "Пожалуйста, включите геолокацию", "ОК");
                    
                    return;
                }



                //timeEndDay = DateTime.Now.TimeOfDay;

                //await DisplayAlert("Уведомление", $"Широта: { endLatitude } Долгота: { endLongitude }", "ОК");

                step = STEP_END_WORK;

                var content = new StringContent(JsonConvert.SerializeObject(new
                {                    
                    geolocation_end = endLatitude + "|" + endLongitude,
                    step = step
                })); 
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PutAsync(ServerUrl.workdays + workingDayId, content);

                

                //string result = await response.Content.ReadAsStringAsync();

                //await DisplayAlert(null, result, "ok");

                dayButton.IsEnabled = false;
                sendbutton.IsEnabled = true;
                runEndEntry.IsEnabled = true;

                //CheckFillElements();

                
            }

            StateOfActivityFrame();

        }

        private async void ButtonDinner_Clicked(object sender, EventArgs e)
        {
            StateOfActivityFrame();

            if (dinnerButton.Text == "Начать обед")
            {

                //логика начала обеда

                step = STEP_DINNER;

                //timeStartDinner = DateTime.Now.TimeOfDay;              

                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    step = step
                }));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PutAsync(ServerUrl.workdays + workingDayId, content);

                

                //string result = await response.Content.ReadAsStringAsync();

                //await DisplayAlert(null, result, "ok");

                dinnerButton.Text = "Окончить обед";


            }

            else
            {
                //логика окончания обеда

                step = STEP_CONTINUE_WORK;

                //timeEndDinner = DateTime.Now.TimeOfDay;

                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    step = step
                }));
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage response = await client.PutAsync(ServerUrl.workdays + workingDayId, content);

                

                //string result = await response.Content.ReadAsStringAsync();

                //await DisplayAlert(null, result, "ok");

                dinnerButton.IsEnabled = false;
                dayButton.IsEnabled = true;
            }

            StateOfActivityFrame();
        }

        private async void ButtonSend_Clicked(object sender, EventArgs e)
        {
            StateOfActivityFrame();

            if (runEndEntry.Text == null)
            {
                StateOfActivityFrame();
                await DisplayAlert("Уведомление", "Пожалуйста, введите пробег на конец дня", "ОК");
                return;
            }

            int startMileage = Convert.ToInt32(runStartEntry.Text);
            int endMileage = Convert.ToInt32(runEndEntry.Text);

            if (endMileage <= startMileage)
            {
                StateOfActivityFrame();
                await DisplayAlert("Ошибка", "Пробег на конец дня не может быть меньше или равен пробегу на начало дня", "ОК");
                return;
            }

            //логика отправки сообщения
            /////////
                      
            //отправка пробега на конец дня на сервер
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                number = cars[numberPicker.SelectedIndex].number,
                mileage = endMileage,
            }));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
         
            HttpResponseMessage response = await client.PutAsync(ServerUrl.carsUrl + cars[numberPicker.SelectedIndex].number + "/", content);

            cars[numberPicker.SelectedIndex].mileage = endMileage;


            //завершаем рабочий день
            content = new StringContent(JsonConvert.SerializeObject(new
            {
                working_day_close_status = true,
            }));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            response = await client.PutAsync(ServerUrl.workdays + workingDayId, content);


            StateOfActivityFrame();

            await DisplayAlert("Спасибо за работу", "Данные успешно отправлены", "ОК");


            Process.GetCurrentProcess().CloseMainWindow();
        }


        private async void numberPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            StateOfActivityFrame();

            await SetWorkingDayStatus();

            //если имеется пробег на начало дня, то устанавливаем его
            if (cars[numberPicker.SelectedIndex].mileage != 0)
            {
                runStartEntry.Text = cars[numberPicker.SelectedIndex].mileage.ToString();
                runStartEntry.IsEnabled = false;
            }
           
            
            StateOfActivityFrame();
        }

        

        //получение геопозиции
        private async Task GetCurrentLocation(typeLocation type)
        {
            CancellationTokenSource cts;
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                cts = new CancellationTokenSource();
                var location = await Geolocation.GetLocationAsync(request, cts.Token);

                if (location != null)
                {
                    //Debug.WriteLine($"Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");
                    if (type == typeLocation.startLocation)
                    {
                        startLatitude = location.Latitude;
                        startLongitude = location.Longitude;
                    }

                    else if (type == typeLocation.endLocation)
                    {
                        endLatitude = location.Latitude;
                        endLongitude = location.Longitude;
                    }

                }


            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.StackTrace);
            }

        }

        //устанавливаем водителя и его статус для включения/отключения кнопок
        private async Task SetWorkingDayStatus()
        {
            await Update_WorkingDays();

            UpdateData();

            foreach (WorkingDay day in workingDays)
            {
                if (day.driver == userId)
                {
                    if (day.car == cars[numberPicker.SelectedIndex].id)
                    {
                        workingDayId = day.id;
                        step = day.step;
                    }
                }
            }

            if (step == STEP_START_DAY)
            {
                dayButton.Text = "Окончить день";
                dinnerButton.IsEnabled = true;
                dayButton.IsEnabled = false;
                runStartEntry.IsEnabled = false;
            }

            else if (step == STEP_DINNER)
            {
                dayButton.Text = "Окончить день";
                dinnerButton.Text = "Окончить обед";
                dinnerButton.IsEnabled = true;
                dayButton.IsEnabled = false;
                runStartEntry.IsEnabled = false;
            }

            else if (step == STEP_CONTINUE_WORK)
            {
                dayButton.Text = "Окончить день";
                dinnerButton.Text = "Окончить обед";
                dinnerButton.IsEnabled = false;
                dayButton.IsEnabled = true;
                runStartEntry.IsEnabled = false;
            }

            else if (step == STEP_END_WORK)
            {
                dayButton.Text = "Окончить день";
                dinnerButton.Text = "Окончить обед";
                dayButton.IsEnabled = false;
                dinnerButton.IsEnabled = false;
                sendbutton.IsEnabled = true;
                runEndEntry.IsEnabled = true;
                runStartEntry.IsEnabled = false;
            }

            
        }

        //обновляем кнопки, ид и шаг для активного водителя
        private void UpdateData()
        {
            workingDayId = 0;
            step = STEP_NO;

            dayButton.IsEnabled = true;
            dinnerButton.IsEnabled = false;
            runStartEntry.IsEnabled = true;
            runEndEntry.IsEnabled = false;
            sendbutton.IsEnabled = false;
            dayButton.Text = "Начать день";
            dinnerButton.Text = "Начать обед";
            
        }

        //переопределение метода нажатия кнопки назад
        protected override bool OnBackButtonPressed()
        {

            AnimationPopup();

            return true;
            
        }

        //управление анимацией всплывающего окна при нажатии кнопки назад
        private async void AnimationPopup()
        {
            

            if (!popupLayout.IsVisible)
            {
                popupLayout.IsVisible = !popupLayout.IsVisible;
                //this.popuplayout.AnchorX = 1;
                //this.popuplayout.AnchorY = 1;

                Animation scaleAnimation = new Animation(
                    f => popupLayout.Scale = f,
                    0,
                    1,
                    Easing.SinInOut);

                Animation fadeAnimation = new Animation(
                    f => popupLayout.Opacity = f,
                    0.2,
                    1,
                    Easing.SinInOut);

                scaleAnimation.Commit(popupLayout, "popupScaleAnimation", 250);
                fadeAnimation.Commit(popupLayout, "popupFadeAnimation", 250);

                //запускаем таймер на 2 секунды для подтверждения выхода из приложения
                Device.StartTimer(TimeSpan.FromSeconds(2), () =>
                {
                    exitTimer = false;

                    AnimationPopup();

                    return false;
                });

                exitTimer = true;
            }
            else
            {
                //если нажимаем на кнопку назад повторно, то выходим из приложения
                if (exitTimer)
                {
                    Process.GetCurrentProcess().CloseMainWindow();
                }

                await Task.WhenAny<bool>
                  (
                    popupLayout.FadeTo(0, 200, Easing.SinInOut)
                  );

                popupLayout.IsVisible = !popupLayout.IsVisible;
            }

                      
        }

        //управление состоянием индикатора загрузки
        private void StateOfActivityFrame()
        {
            if (activityFrame.IsVisible)
            {
                activityFrame.IsVisible = false;
                return;
            }

            else
            {
                activityFrame.IsVisible = true;
                return;
            }
        }

        //обновляем рабочие дни, берем данные с сервера
        private async Task Update_WorkingDays()
        {
            workingDays.Clear();

            var response = await client.GetAsync(ServerUrl.active_drivers);
            string result = await response.Content.ReadAsStringAsync();


            workingDays = JsonConvert.DeserializeObject<List<WorkingDay>>(result);
        }

        


    }
}