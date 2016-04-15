﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceStack
{
    public static class ServiceGatewayExtensions
    {
        public static TResponse Send<TResponse>(this IServiceGateway client, IReturn<TResponse> request)
        {
            return client.Send<TResponse>(request);
        }

        public static void Send(this IServiceGateway client, IReturnVoid request)
        {
            client.Send<byte[]>(request);
        }

        public static List<TResponse> SendAll<TResponse>(this IServiceGateway client, IEnumerable<IReturn<TResponse>> request)
        {
            return client.SendAll<TResponse>(request);
        }

        public static object Send(this IServiceGateway client, Type resposneType, object request)
        {
            Func<IServiceGateway, object, object> sendFn;
            if (!LateBoundSendSyncFns.TryGetValue(resposneType, out sendFn))
            {
                var mi = typeof(ServiceGatewayExtensions).GetMethod("SendObject", BindingFlags.Static | BindingFlags.NonPublic);
                var genericMi = mi.MakeGenericMethod(resposneType);
                LateBoundSendSyncFns[resposneType] = sendFn = (Func<IServiceGateway, object, object>)
                    genericMi.CreateDelegate(typeof(Func<IServiceGateway, object, object>));
            }
            return sendFn(client, request);
        }

        public static Task<object> SendAsync(this IServiceGateway client, Type resposneType, object request, CancellationToken token=default(CancellationToken))
        {
            Func<IServiceGateway, object, CancellationToken, Task<object>> sendFn;
            if (!LateBoundSendAsyncFns.TryGetValue(resposneType, out sendFn))
            {
                var mi = typeof(ServiceGatewayExtensions).GetMethod("SendObjectAsync", BindingFlags.Static | BindingFlags.NonPublic);
                var genericMi = mi.MakeGenericMethod(resposneType);
                LateBoundSendAsyncFns[resposneType] = sendFn = (Func<IServiceGateway, object, CancellationToken, Task<object>>)
                    genericMi.CreateDelegate(typeof(Func<IServiceGateway, object, CancellationToken, Task<object>>));
            }
            return sendFn(client, request, token);
        }

        public static Type GetResponseType(this IServiceGateway client, object request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            var returnTypeDef = request.GetType().GetTypeWithGenericTypeDefinitionOf(typeof(IReturn<>));
            if (returnTypeDef == null)
                throw new ArgumentException("Late-bound Send<object> can only be called for Request DTO's implementing IReturn<T>");

            var resposneType = returnTypeDef.GetGenericArguments()[0];
            return resposneType;
        }

        private static readonly ConcurrentDictionary<Type, Func<IServiceGateway, object, object>> LateBoundSendSyncFns =
            new ConcurrentDictionary<Type, Func<IServiceGateway, object, object>>();

        internal static object SendObject<TResponse>(IServiceGateway client, object request)
        {
            return client.Send<TResponse>(request);
        }

        private static readonly ConcurrentDictionary<Type, Func<IServiceGateway, object, CancellationToken, Task<object>>> LateBoundSendAsyncFns =
            new ConcurrentDictionary<Type, Func<IServiceGateway, object, CancellationToken, Task<object>>>();

        internal static Task<object> SendObjectAsync<TResponse>(IServiceGateway client, object request, CancellationToken token)
        {
            return client.SendAsync<TResponse>(request, token).ContinueWith(x => (object)x.Result, token);
        }
    }

    public static class ServiceGatewayAsyncWrappers
    {
        public static Task<TResponse> SendAsync<TResponse>(this IServiceGateway client, IReturn<TResponse> requestDto, CancellationToken token = default(CancellationToken))
        {
            return client.SendAsync<TResponse>((object)requestDto, token);
        }

        public static Task<TResponse> SendAsync<TResponse>(this IServiceGateway client, object requestDto, CancellationToken token = default(CancellationToken))
        {
            var nativeAsync = client as IServiceGatewayAsync;
            return nativeAsync != null
                ? nativeAsync.SendAsync<TResponse>(requestDto, token)
                : Task.Factory.StartNew(() => client.Send<TResponse>(requestDto), token);
        }

        public static Task SendAsync(this IServiceGateway client, IReturnVoid requestDto, CancellationToken token = default(CancellationToken))
        {
            var nativeAsync = client as IServiceGatewayAsync;
            return nativeAsync != null
                ? nativeAsync.SendAsync<byte[]>(requestDto, token)
                : Task.Factory.StartNew(() => client.Send<byte[]>(requestDto), token);
        }

        public static Task<List<TResponse>> SendAllAsync<TResponse>(this IServiceGateway client, IEnumerable<IReturn<TResponse>> requestDtos, CancellationToken token = default(CancellationToken))
        {
            var nativeAsync = client as IServiceGatewayAsync;
            return nativeAsync != null
                ? nativeAsync.SendAllAsync<TResponse>(requestDtos, token)
                : Task.Factory.StartNew(() => client.SendAll<TResponse>(requestDtos), token);
        }

        public static Task PublishAsync(this IServiceGateway client, object requestDto, CancellationToken token = default(CancellationToken))
        {
            var nativeAsync = client as IServiceGatewayAsync;
            return nativeAsync != null
                ? nativeAsync.PublishAsync(requestDto, token)
                : Task.Factory.StartNew(() => client.Publish(requestDto), token);
        }

        public static Task PublishAllAsync(this IServiceGateway client, IEnumerable<object> requestDtos, CancellationToken token = default(CancellationToken))
        {
            var nativeAsync = client as IServiceGatewayAsync;
            return nativeAsync != null
                ? nativeAsync.PublishAllAsync(requestDtos, token)
                : Task.Factory.StartNew(() => client.PublishAll(requestDtos), token);
        }
    }
}