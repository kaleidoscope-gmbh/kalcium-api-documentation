# kalcium-api-documentation

This page provides documentation and sample code to access [Kalcium server](http://www.quickterm.at/) developed by [Kaleidoscope GmbH](http://www.kaleidoscope.at). The intended audience of this page is developers who write custom clients. 

The Kalcium platform's own frontend uses the very same API. 

## Kalcium REST API

* You can communicate via a Kalcium server with a RESTful API. 
* If you have a Kalcium server running, you can check its public API using this address: `http://<server-address>/kalcrest/swagger`. 
* The responses to the authenticated user's requests are in [JSON format](https://www.json.org/).

## Client development in .NET

To facilitate the development, we publish a [nuget package `(Kaleidoscope.Kalcium.Client)`](https://www.nuget.org/packages/Kaleidoscope.Kalcium.Client/) that provides a class library to access Kalcium. 
* Note that this library uses the Kalcium REST API via HTTP calls.
* The platform of the published class library is [.NET standard to support multiple platforms](https://docs.microsoft.com/en-us/dotnet/standard/net-standard). 
* The Kalcium API changes over different versions.  Always use the appropriate nuget package based on the version of the server you want to connect.

## Examples

This repository contains sample code in the folder `test-client` to illustrate how to use the published API via the nuget package in a simple .NET Core console application. 


