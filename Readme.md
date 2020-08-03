## Weather Log Worker Service
- This project makes API call openweathermap.org to get weather data for Lagos City, and thereafter logs the temperature to a backend API

## Run Locally
- Pull this repository
- Setup appsettings.json in WorkerServiceProject directory following the example in `WorkerServiceProject/appsettings.sample.json`
- Build the project and run locally, you can also publish to Windows service using VS code.
- The worker service run at an interval of 5 minutes and log the API call info in `WorkerServiceProject/Logfile.txt`



