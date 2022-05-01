using Newtonsoft.Json;
using Plugin.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace TestXamarin
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Page1 : ContentPage
    {
        private NetworkAccess current;

        private readonly HttpClient client = new HttpClient();

        

        public static string login;
        public static string password;
        

        public Page1()
        {
            InitializeComponent();        
        }

        protected override async void OnAppearing()
        {
            await Internet_Connect();         

            

            //StateOfActivityFrame();

            login = CrossSettings.Current.GetValueOrDefault("login", null);
            loginEntry.Text = login;


            password = CrossSettings.Current.GetValueOrDefault("password", null);
            passwordEntry.Text = password;


            if (password != null)
            {
                Button_Clicked(new object(), new EventArgs());

                
            }

            // StateOfActivityFrame();

            base.OnAppearing();

        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            await Internet_Connect();

            StateOfActivityFrame();

            if (loginEntry.Text != null && passwordEntry.Text != null)
            {

                login = loginEntry.Text.Trim();
                password = passwordEntry.Text.Trim();
                     
                //логика прохождения авторизации

                //Dictionary<string, string> dict = new Dictionary<string, string>
                //{
                //    { "", "" }
                //};

                //FormUrlEncodedContent form = new FormUrlEncodedContent(dict);

                //HttpResponseMessage response = await client.PostAsync(ServerUrl.driversUrl, form);

                //string result = await response.Content.ReadAsStringAsync();



                //var content = new StringContent(JsonConvert.SerializeObject(new { login = login, password = password }));
                //content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                //HttpResponseMessage response = await client.PostAsync(ServerUrl.driversUrl, content);

                //string result = await response.Content.ReadAsStringAsync();

                //await DisplayAlert(null, result, "ok");



                //4shFXwD4YYAM5uE

                //устанавливаем логин и пароль на сервер
                var authData = string.Format("{0}:{1}", login, password);
                var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(authData));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);


                //ищем водителя по логину
                HttpResponseMessage response = await client.GetAsync(ServerUrl.driversUrl + login + "/");
                string result = await response.Content.ReadAsStringAsync();

                

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {

                   

                    //var drivers = JsonConvert.DeserializeObject<List<Driver>>(result);

                    //делаем преобразования и устанавливаем логин и пароль на вторую страницу
                    char[] simv = { '[', ']', '{', '}', '\"' };

                    foreach (char c in simv)
                    {
                        result = result.Replace(c.ToString(), "");
                    }

                    string[] words = result.Split(',');

                    string[] idWords = words[0].Split(':');
                    Page2.userId = Convert.ToInt32(idWords[1]);

                    string[] usernameWords = words[2].Split(':');
                    Page2.userName = usernameWords[1];

                    Page2.login = login;
                    Page2.password = password;

                    Page2.firstLaunch = true;

                    CrossSettings.Current.AddOrUpdateValue("login", login);
                    CrossSettings.Current.AddOrUpdateValue("password", password);

                    password = null;

                    await Navigation.PushAsync(new Page2());
                    //App.Current.MainPage = new Page2();

                    passwordEntry.Text = null;
                }

                else
                {
                    await DisplayAlert("Ошибка авторизации", "Логин или пароль введены неверно\nОбратите внимание на язык и регистр", "ОК");
                }

                

            }

            else
            {
                await DisplayAlert("Ошибка авторизации", "Введены не все данные", "ОК");
            }

            StateOfActivityFrame();

        }

        //проверка подключения к интернету
        private async Task Internet_Connect()
        {
            bool noInternet = true;

            while (noInternet)
            {
                current = Connectivity.NetworkAccess;

                if (current == NetworkAccess.None)
                {
                    await DisplayAlert("Нет подключения к интернету", "Пожалуйста, включите сеть и закройте это окно", "ОК");
                }

                else
                {
                    noInternet = false;
                }
            }
        }

        //Управление индикатором загрузки
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

    }
}