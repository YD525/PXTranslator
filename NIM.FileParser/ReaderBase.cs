using PhoenixEngine.Memory;

namespace ModFileParser
{ 
    public interface IModReader <T>
    {
        /// <summary>
        /// Create an instance; `Create` must be executed after `new` is completed.
        /// </summary>
        /// <param name="FileUniqueKey">The file ID. It can be obtained using a function in Phoenix.</param>
        /// <param name="Link">The first parameter of `P_Dict<string, P_String>` is the `UniqueKey`, which corresponds to the `UniqueKey` in the return value of the `Load` method; the second parameter represents the value to be modified.</param>
        void Create(int FileUniqueKey, P_Dict<string, P_String> Link);

        /// <summary>
        /// The `Load` method returns a `Dictionary<string, object>` where the `string` serves as a `UniqueKey` that needs to be saved.
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        T Load(string Path);

        /// <summary>
        /// Execute the save method to save to the current directory.
        /// </summary>
        /// <param name="ModifyCount">Number of affected rows.</param>
        /// <returns></returns>
        bool Save(ref int ModifyCount);
    }

    public interface IScriptReader<T>
    {
        /// <summary>
        /// Create an instance; `Create` must be executed after `new` is completed.
        /// </summary>
        /// <param name="FileUniqueKey">The file ID. It can be obtained using a function in Phoenix.</param>
        /// <param name="Link">The first parameter of `P_Dict<string, P_String>` is the `UniqueKey`, which corresponds to the `UniqueKey` in the return value of the `Load` method; the second parameter represents the value to be modified.</param>
        void Create(int FileUniqueKey, P_Dict<string, P_String> Link);

        /// <summary>
        /// The `Load` method returns a `Dictionary<string, object>` where the `string` serves as a `UniqueKey` that needs to be saved.
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        T Load(CodeGenStyle Style, bool ShowAssembly,string Path);

        /// <summary>
        /// Execute the save method to save to the current directory.
        /// </summary>
        /// <param name="ModifyCount">Number of affected rows.</param>
        /// <returns></returns>
        bool Save(ref int ModifyCount);
    }

}
