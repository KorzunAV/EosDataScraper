using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Cryptography.ECDSA;
using Ditch.EOS.Models;
using EosDataScraper.Models;
using Newtonsoft.Json.Linq;

namespace EosDataScraper.Extensions
{
    public static class JsonExtension
    {
        public static BaseAction ToActionOrNull(this JToken action, GetBlockResults blockResults, string id, StatusEnum status, int actionNum, System.DateTime expiration)
        {
            try
            {
                var account = action.Value<string>("account");
                var name = action.Value<string>("name");
                var data = action.Value<JToken>("data");

                BaseAction a;
                switch (name)
                {
                    case TransferAction.ActionKey when data.Type == JTokenType.Object:
                        {
                            var transfer = data.ToTransferAction();
                            if (transfer == null || !IsValid(transfer))
                                goto default;

                            if (status == StatusEnum.Executed)
                                transfer.Timestamp = blockResults.Timestamp.Value;
                            
                            a = transfer;
                            break;
                        }
                    case TokenAction.ActionKey when data.Type == JTokenType.Object:
                        {
                            a = data.ToTokenAction();
                            if (a == null || !IsValid(a))
                                goto default;
                            break;
                        }
                    default:
                        {
                            return null;
                        }
                }

                a.TransactionStatus = status;
                a.ActionNum = actionNum;
                a.BlockNum = blockResults.BlockNum;
                a.ActionContract = account;
                a.ActionName = name;
                a.TransactionId = Hex.HexToBytes(id);
                a.TransactionExpiration = expiration;
                return a;
            }
            catch
            {
                return null;
            }
        }

        private static TokenAction ToTokenAction(this JToken data)
        {
            return data.ToObject<TokenAction>();
        }

        private static TransferAction ToTransferAction(this JToken data)
        {
            return data.ToObject<TransferAction>();
        }

        private static bool IsValid<T>(T instance)
        {
            if (instance == null)
                return false;

            var results = new List<ValidationResult>();
            var context = new ValidationContext(instance);
            Validator.TryValidateObject(instance, context, results);
            return !results.Any();
        }
    }
}
