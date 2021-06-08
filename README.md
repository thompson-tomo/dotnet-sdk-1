# dotnet-sdk

This is the .NET SDK for both single-user client side and multi-user server side usage. Use `Statsig.Client` for client side and `Statsig.Server` for server side.

## The Basics

Get started in a few quick steps.

1. [Create a free account on statsig.com](#step1)
2. [Install the SDK](#step2)
3. [Initialize and use the SDK](#step3)

<a name="step1"></a>

#### Step 1 - Create a free account on [www.statsig.com](https://console.statsig.com/sign_up)

You could skip this for now, but you will need an SDK Key and some Feature Gates or Dynamic Configs to use with the SDK in just a minute.

<a name="step2"></a>

#### Step 2 - Install the SDK

The package is hosted on [Nuget](https://www.nuget.org/packages/Statsig/). You can either install it from your Visual Studio's Nuget package manager, or through .NET CLI:

```
dotnet add package Statsig --version 0.1.0
```

#### Step 3 - Import and use the SDK

Refer to our [client side](https://docs.statsig.com/client/dotnetSDK) or [server side](https://docs.statsig.com/server/dotnetSDK) SDK docs on how to use the SDK.
