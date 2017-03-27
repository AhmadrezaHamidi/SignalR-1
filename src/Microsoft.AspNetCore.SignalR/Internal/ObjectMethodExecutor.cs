// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.SignalR.Internal
{
    internal class ObjectMethodExecutor
    {
        private readonly object[] _parameterDefaultValues;
        private readonly ActionExecutor _executor;

        private static readonly MethodInfo _convertOfTMethod =
            typeof(ObjectMethodExecutor).GetRuntimeMethods().Single(methodInfo => methodInfo.Name == nameof(ObjectMethodExecutor.Convert));
        private static readonly MethodInfo _fromAwaitableMethod =
            typeof(Observable).GetRuntimeMethods().Single(methodInfo => methodInfo.Name == nameof(Observable.FromAwaitable));
        private static readonly MethodInfo _fromInvocationMethod =
            typeof(Observable).GetRuntimeMethods().Single(methodInfo => methodInfo.Name == nameof(Observable.FromInvocation));
        private static readonly PropertyInfo _completedObservableProperty =
            typeof(Observable).GetRuntimeProperties().Single(propertyInfo => propertyInfo.Name == nameof(Observable.Completed));

        private ObjectMethodExecutor(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }

            MethodInfo = methodInfo;
            TargetTypeInfo = targetTypeInfo;
            MethodParameters = methodInfo.GetParameters();

            _executor = GetExecutor(methodInfo, targetTypeInfo);
            _parameterDefaultValues = GetParameterDefaultValues(MethodParameters);
        }

        private delegate IObservable<object> ActionExecutor(object target, object[] parameters);

        public MethodInfo MethodInfo { get; }

        public TypeInfo TargetTypeInfo { get; }

        public ParameterInfo[] MethodParameters { get; }

        public static ObjectMethodExecutor Create(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            var executor = new ObjectMethodExecutor(methodInfo, targetTypeInfo);
            return executor;
        }

        public IObservable<object> Execute(object target, object[] parameters)
        {
            return _executor(target, parameters);
        }

        public object GetDefaultValueForParameter(int index)
        {
            if (index < 0 || index > MethodParameters.Length - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _parameterDefaultValues[index];
        }

        private static ActionExecutor GetExecutor(MethodInfo methodInfo, TypeInfo targetTypeInfo)
        {
            // Parameters to executor
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            // Build parameter list
            var parameters = new List<Expression>();
            var paramInfos = methodInfo.GetParameters();
            for (int i = 0; i < paramInfos.Length; i++)
            {
                var paramInfo = paramInfos[i];
                var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                var valueCast = Expression.Convert(valueObj, paramInfo.ParameterType);

                // valueCast is "(Ti) parameters[i]"
                parameters.Add(valueCast);
            }

            // Call method
            MethodCallExpression methodCall;

            if (!methodInfo.IsStatic)
            {
                var instanceCast = Expression.Convert(targetParameter, targetTypeInfo.AsType());
                methodCall = Expression.Call(instanceCast, methodInfo, parameters);
            }
            else
            {
                methodCall = Expression.Call(null, methodInfo, parameters);
            }

            var returnType = methodCall.Type.GetTypeInfo();

            // methodCall is "((Ttarget) target) method((T0) parameters[0], (T1) parameters[1], ...)"
            // Create function
            if (methodCall.Type == typeof(void))
            {
                // Create an immediately-completing observable with no results
                var body = Expression.Block(
                    methodCall,
                    Expression.Property(null, _completedObservableProperty));

                var lambda = Expression.Lambda<ActionExecutor>(body, targetParameter, parametersParameter);
                return lambda.Compile();
            }
            else if (IsObservable(returnType))
            {
                var body = Expression.Convert(methodCall, typeof(IObservable<object>));
                var lambda = Expression.Lambda<ActionExecutor>(body, targetParameter, parametersParameter);
                return lambda.Compile();
            }
            else if (IsAwaitable(methodCall.Type, out var awaiterType, out var awaiterReturnType, out var getAwaiterMethod, out var isCompletedProperty, out var getResultMethod, out var onCompletedMethod))
            {
                var awaiterParam = Expression.Parameter(typeof(object));
                var isCompletedLambda = Expression.Lambda<Func<object, bool>>(Expression.Property(Expression.Convert(awaiterParam, awaiterType), "IsCompleted"), awaiterParam);

                var getResultCall = Expression.Call(Expression.Convert(awaiterParam, awaiterType), getResultMethod);
                var getResultLambda = awaiterReturnType == typeof(void) ?
                    Expression.Lambda<Func<object, object>>(Expression.Block(getResultCall, Expression.Constant(null, typeof(object))), awaiterParam) :
                    Expression.Lambda<Func<object, object>>(Expression.Convert(getResultCall, typeof(object)), awaiterParam);

                var actionParam = Expression.Parameter(typeof(Action));
                var onCompletedLambda = Expression.Lambda<Action<object, Action>>(
                    Expression.Call(Expression.Convert(awaiterParam, awaiterType), onCompletedMethod, actionParam), awaiterParam, actionParam);

                var awaiter = Expression.Variable(typeof(object));
                var body = Expression.Call(_fromAwaitableMethod,
                    Expression.Constant(awaiterReturnType),
                    Expression.Convert(Expression.Call(methodCall, getAwaiterMethod), typeof(object)),
                    isCompletedLambda,
                    getResultLambda,
                    onCompletedLambda);
                var lambda = Expression.Lambda<ActionExecutor>(body, targetParameter, parametersParameter);
                return lambda.Compile();
            }
            else
            {
                // must coerce methodCall to match ActionExecutor signature
                var castMethodCall = Expression.Convert(methodCall, typeof(object));

                // and wrap in observable
                var asObservable = Expression.Call(_fromInvocationMethod, Expression.Lambda<Func<object>>(castMethodCall));

                var lambda = Expression.Lambda<ActionExecutor>(asObservable, targetParameter, parametersParameter);
                return lambda.Compile();
            }
        }

        private static bool IsAwaitable(Type type, out Type awaiterType, out Type returnType, out MethodInfo getAwaiterMethod, out PropertyInfo isCompletedProperty, out MethodInfo getResultMethod, out MethodInfo onCompletedMethod)
        {
            // Based on Roslyn code: http://source.roslyn.io/#Microsoft.CodeAnalysis.Workspaces/Shared/Extensions/ISymbolExtensions.cs,db4d48ba694b9347

            // object GetAwaiter();
            getAwaiterMethod = type.GetRuntimeMethods().FirstOrDefault(m => m.Name.Equals("GetAwaiter"));
            if (getAwaiterMethod == null)
            {
                awaiterType = null;
                isCompletedProperty = null;
                onCompletedMethod = null;
                getResultMethod = null;
                returnType = null;
                return false;
            }

            awaiterType = getAwaiterMethod.ReturnType;
            if (awaiterType == null)
            {
                isCompletedProperty = null;
                onCompletedMethod = null;
                getResultMethod = null;
                returnType = null;
                return false;
            }

            // bool IsCompleted { get; }
            isCompletedProperty = awaiterType.GetRuntimeProperties().FirstOrDefault(p => p.Name.Equals("IsCompleted") && p.PropertyType == typeof(bool) && p.GetMethod != null);
            if (isCompletedProperty == null)
            {
                onCompletedMethod = null;
                getResultMethod = null;
                returnType = null;
                return false;
            }

            onCompletedMethod = awaiterType.GetRuntimeMethods().FirstOrDefault(m => m.Name.Equals("OnCompleted") && m.ReturnType == typeof(void) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Action));
            if(onCompletedMethod == null)
            {
                getResultMethod = null;
                returnType = null;
                return false;
            }

            // void GetResult; || T GetResult();
            getResultMethod = awaiterType.GetRuntimeMethods().FirstOrDefault(m => m.Name.Equals("GetResult") && m.GetParameters().Length == 0);
            if(getResultMethod == null)
            {
                returnType = null;
                return false;
            }

            returnType = getResultMethod.ReturnType;
            return true;
        }

        private static bool IsObservable(TypeInfo type)
        {
            return type.ImplementedInterfaces.Any(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IObservable<>));
        }

        /// <summary>
        /// Cast Task of T to Task of object
        /// </summary>
        private static async Task<object> CastToObject<T>(Task<T> task)
        {
            return (object)await task;
        }

        private static Type GetTaskInnerTypeOrNull(Type type)
        {
            var genericType = ClosedGenericMatcher.ExtractGenericInterface(type, typeof(Task<>));

            return genericType?.GenericTypeArguments[0];
        }

        private static Task<object> Convert<T>(object taskAsObject)
        {
            var task = (Task<T>)taskAsObject;
            return CastToObject<T>(task);
        }

        private static object[] GetParameterDefaultValues(ParameterInfo[] parameters)
        {
            var values = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterInfo = parameters[i];
                object defaultValue;

                if (parameterInfo.HasDefaultValue)
                {
                    defaultValue = parameterInfo.DefaultValue;
                }
                else
                {
                    var defaultValueAttribute = parameterInfo
                        .GetCustomAttribute<DefaultValueAttribute>(inherit: false);

                    if (defaultValueAttribute?.Value == null)
                    {
                        defaultValue = parameterInfo.ParameterType.GetTypeInfo().IsValueType
                            ? Activator.CreateInstance(parameterInfo.ParameterType)
                            : null;
                    }
                    else
                    {
                        defaultValue = defaultValueAttribute.Value;
                    }
                }

                values[i] = defaultValue;
            }

            return values;
        }
    }
}
