using Game.Server.Auth;
using Game.Server.Chat;
using Game.Server.MultiGameplay;
using Game.Server.User;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Server.Lobbies;
using Server.Network;

namespace Game.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // gRPC 서버 설정  +  의존성 주입 (DI)
            //==========================================================

            // Kestrel 웹 서버 구성. Http2 전용 포트 설정
            builder.WebHost.ConfigureKestrel(option =>
            {
                option.ListenAnyIP(7777, listenOption => listenOption.Protocols = HttpProtocols.Http2);
            });

            // gRPC 서비스 등록 및 설정
            builder.Services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = true; // 개발용 (디버깅용)
            });
            builder.Services.AddGrpcReflection();
            builder.Services.AddSingleton<IdGenerator>(); // singleton 의존성 주입 객체  (DI 컨테이너에 등록한다)
            builder.Services.AddSingleton<LobbiesManager>();
            builder.Services.AddSingleton<MultiGameServerManager>();
            // AddScoped : gRPC 호출단위

            var app = builder.Build();

            // gRPC Endpoints
            //=========================================================

            app.MapGrpcService<AuthServiceImpl>();
            app.MapGrpcService<UserServiceImpl>();
            app.MapGrpcService<ChatServiceImpl>();
            app.MapGrpcService<LobbiesServiceImpl>();
            app.MapGrpcService<MultiGameplayServiceImpl>();
            app.MapGrpcReflectionService();
            app.MapGet("/", () => "gRPC server running on port 7777 (HTTP/2)");
            Console.WriteLine("gRPC server listening on http://0.0.0.0:7777 (HTTP/2)");
            await app.RunAsync();


            // winget 설치 (Powershell)
            // 1. 설치확인
            // Get-AppxPackage Microsoft.DesktopAppInstaller
            //
            // 아무것도 안뜨면 다음으로 설치
            // Invoke-WebRequest https://aka.ms/getwinget -OutFile AppInstaller.msixbundle
            // Add-AppxPackage .\AppInstaller.msixbundle
            //
            // 관리자모드 Powershell 에서 
            // winget --version 으로 설치확인
            // winget install curl.curl
            // winget install fullstorydev.grpcurl
        }
    }
}
