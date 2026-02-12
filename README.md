## 🚀 Getting Started

This project is built with **.NET 10** (C#). Follow the steps below to build and run the application locally.

### Prerequisites

* [.NET SDK](https://dotnet.microsoft.com/download) (Current LTS recommended)
* A code editor like **VS Code**, **Visual Studio**, or **JetBrains Rider**.

### 🛠️ Build and Run

#### 1. Restore Dependencies

First, ensure all necessary packages are downloaded:

```bash
dotnet restore

```

#### 2. Build the Project

Compile the application to check for any errors:

```bash
dotnet build

```

#### 3. Run the Application

You can run the project using the profiles defined in `launchSettings.json`.

* **Option A: Run with HTTPS (Default)**
  This will use the URL `https://localhost:7028`.
```bash
dotnet run --launch-profile "https"

```


* **Option B: Run with HTTP**
  This will use the URL `http://localhost:5218`.
```bash
dotnet run --launch-profile "http"

```

---

### 📖 API Documentation (Swagger)

This project includes **Swagger UI**, which allows you to visualize and interact with the API’s resources without needing to write any frontend code.

When the application is running in the `Development` environment, you can access the interactive documentation at:

* **Swagger UI Api-Key:** ```dev```

* **URL:** `https://localhost:7028/swagger/index.html` (or `http://localhost:5218/swagger/index.html`)

**How to use it:**

1. Ensure the application is running via `dotnet run`.
2. Navigate to the URL above in your browser.
3. Click on any endpoint to see the required parameters and click **"Try it out"** to send a live request to your local server.

---

### 💡 Pro-Tips for the User

* **Hot Reload:** If you want the app to automatically restart when you make code changes, use `dotnet watch` instead of `dotnet run`.
* **Trust HTTPS:** If you get a privacy warning in your browser while using the HTTPS profile, run this command to trust the local development certificate:
```bash
dotnet dev-certs https --trust

```

### 🌐 Accessing the App

Once the application is running, open your browser and navigate to:

* **Main URL:** `https://localhost:7028`
* **Environment:** `Development`

---

## Projects Key Features
- **Smart Caching:** Optimized for static or infrequently changing data from the Hacker News API.
  - Used Immutable Collections since the data is managed by a third party and the internal API only requires read access.
  - Implemented an asynchronous update cadence (every few minutes), assuming the "Best Stories" list does not require real-time parity.
- **Background Services:** Utilized BackgroundService to handle out-of-band cache refreshes without blocking incoming requests.
- **Thread-Safe Synchronization:** Demonstrated advanced .NET synchronization using Interlocked.Exchange to swap Immutable Collection references once the background worker completes a refresh.
- **Concurrency Processing:**** Implemented Parallel Programming to fetch multiple story details simultaneously.
  - Configured Retry policies, Exponential Backoff, and Max Degree of Parallelism for outbound calls.
- **API Resiliency & Rate Limiting:** Integrated inbound rate limiting strategies to protect the service from exhaustion.
- **Custom Middleware:** Implemented an API-Key based authorization layer designed for future extensibility.
- **Performance Optimization:** Enabled high-speed JSON serialization by disabling dynamic reflection (utilizing source generators or pre-defined schemas).
- **Developer Experience:** Integrated a clean Swagger UI, structured logging with Scopes, and Debug.Assert for critical state validation.
- **Lean Architecture:** Used the Minimal API pattern to reduce the binary footprint and keep the feature set focused.
- **Testing:** Included a suite of Unit Tests to ensure core logic reliability.

### Potential Improvements
- **Granular Rate Limiting:** Transition from global rate limiting to API-Key based limits.
- **Cold-Start Cache Population:** Currently, the first request may find an empty cache. I plan to implement startup pre-warming to ensure the cache is populated before the API begins accepting traffic.
- **Enhanced Traceability:** Introduce a Scoped Metadata Model to carry Correlation IDs, Request IDs, and User IDs across logs for better observability.
- **Test Coverage:** Further expand unit and integration tests to cover edge cases in the background worker synchronization.