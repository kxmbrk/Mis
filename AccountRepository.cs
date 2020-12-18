using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Playground.Core.AdoNet;
using Playground.Core.Utilities;
using Playground.WpfApp.Forms.DataGridsEx.AccountMgr;

namespace Playground.WpfApp.Repositories
{
    public interface IAccountRepository
    {
        List<CategoryModel> GetAllCategories();

        List<AccountModel> GetAccountsByCategoryId(int categoryId);

        int InsertNewAccountCategory(string newCategoryName);

        void UpdateAccountCategory(int categoryId, string newCategoryName);

        void DeleteAccountCategory(int categoryId);

        bool IsCategoryAlreadyExist(string categoryName);

        bool CategoryHasChildren(int categoryId);

        AccountModel GetAccountByAccountId(int accountId);

        int InsertNewAccount(AccountModel newAcctModel);

        void DeleteAccount(int accountId);

        void UpdateAccount(AccountModel updatedAcctModel);

        string GetPassword(int accountId);
    }

    public class AccountRepository : IAccountRepository
    {
        private string _sql = string.Empty;

        public bool CategoryHasChildren(int categoryId)
        {
            bool retVal = false;

            object obj = DAL.Kashif.ExecuteScalar("SELECT COUNT(*) FROM ACCT_MGR WHERE CATEGORY_ID = " + categoryId);

            if (Convert.ToInt32(obj) > 0)
            {
                retVal = true;
            }

            return retVal;
        }

        public void DeleteAccount(int accountId)
        {
            DAL.Kashif.ExecuteNonQuery("DELETE FROM ACCT_MGR WHERE ID = " + accountId);
        }

        public void DeleteAccountCategory(int categoryId)
        {
            var anonymousBlock = new StringBuilder();
            anonymousBlock.AppendLine("DECLARE");
            anonymousBlock.AppendLine($@"V_CATEGORY_ID NUMBER := {categoryId};");
            anonymousBlock.AppendLine("BEGIN");
            anonymousBlock.AppendLine("DELETE FROM ACCT_MGR WHERE CATEGORY_ID = V_CATEGORY_ID;");
            anonymousBlock.AppendLine("DELETE FROM ACCT_CATEGORY WHERE CATEGORY_ID = V_CATEGORY_ID;");
            anonymousBlock.AppendLine("COMMIT;");
            anonymousBlock.AppendLine("END;");

            DAL.Kashif.ExecuteNonQuery(anonymousBlock.ToString());
        }

        public bool IsCategoryAlreadyExist(string categoryName)
        {
            var count = Convert.ToInt32(DAL.Kashif.ExecuteScalar($@"
                        SELECT COUNT(*) 
                        FROM 
                            ACCT_CATEGORY
                        WHERE CATEGORY_NAME = '{categoryName.Trim()}'"));
            return count > 0;
        }

        public AccountModel GetAccountByAccountId(int accountId)
        {
            var retVal = new AccountModel();
            var dt = DAL.Kashif.ExecuteQuery($"SELECT * FROM ACCT_MGR WHERE ID = {accountId} ORDER BY ACCT_NAME");

            if (dt.Rows.Count == 0) return null;

            var decryptedPassword = Encryption.Decrypt(dt.Rows[0]["ACCT_PASSWORD"].ToString());

            retVal.AccountId = Convert.ToInt32(dt.Rows[0]["ID"]);
            retVal.AccountName = dt.Rows[0]["ACCT_NAME"].ToString();
            retVal.AccountLoginId = dt.Rows[0]["ACCT_LOGIN_ID"].ToString();
            retVal.AccountPassword = string.IsNullOrEmpty(decryptedPassword)
                ? dt.Rows[0]["ACCT_PASSWORD"].ToString()
                : decryptedPassword;
            retVal.Notes = dt.Rows[0]["ACCT_NOTES"].ToString();
            retVal.CategoryId = Convert.ToInt32(dt.Rows[0]["CATEGORY_ID"]);

            if (dt.Rows[0]["DATE_CREATED"] != null && dt.Rows[0]["DATE_CREATED"] != DBNull.Value)
            {
                retVal.DateCreated = Convert.ToDateTime(dt.Rows[0]["DATE_CREATED"]);
            }

            if (dt.Rows[0]["DATE_MODIFIED"] != null && dt.Rows[0]["DATE_MODIFIED"] != DBNull.Value)
            {
                retVal.DateModified = Convert.ToDateTime(dt.Rows[0]["DATE_MODIFIED"]);
            }

            retVal.IsPasswordEncrypted = dt.Rows[0]["IS_PASSWORD_ENCRYPTED"].ToString();

            return retVal;
        }

        public List<AccountModel> GetAccountsByCategoryId(int categoryId)
        {
            var retVal = new List<AccountModel>();
            var dt = DAL.Kashif.ExecuteQuery($"SELECT * FROM ACCT_MGR WHERE CATEGORY_ID = {categoryId} ORDER BY ACCT_NAME");

            if (dt.Rows.Count == 0) return null;

            foreach (DataRow row in dt.Rows)
            {
                var model = new AccountModel
                {
                    AccountId = Convert.ToInt32(row["ID"]),
                    AccountName = row["ACCT_NAME"].ToString(),
                    AccountLoginId = row["ACCT_LOGIN_ID"].ToString(),
                    Notes = row["ACCT_NOTES"].ToString(),
                    CategoryId = Convert.ToInt32(row["CATEGORY_ID"])
                };

                var decryptedPassword = Encryption.Decrypt(row["ACCT_PASSWORD"].ToString());

                model.AccountPassword = string.IsNullOrEmpty(decryptedPassword)
                    ? row["ACCT_PASSWORD"].ToString()
                    : decryptedPassword;
                
                if (row["DATE_CREATED"] != null && row["DATE_CREATED"] != DBNull.Value)
                {
                    model.DateCreated = Convert.ToDateTime(row["DATE_CREATED"]);
                }

                if (row["DATE_MODIFIED"] != null && row["DATE_MODIFIED"] != DBNull.Value)
                {
                    model.DateModified = Convert.ToDateTime(row["DATE_MODIFIED"]);
                }

                model.IsPasswordEncrypted = row["IS_PASSWORD_ENCRYPTED"].ToString();

                retVal.Add(model);
            }

            return retVal;
        }

        public List<CategoryModel> GetAllCategories()
        {
            var retVal = new List<CategoryModel>();
            var dt = DAL.Kashif.ExecuteQuery("SELECT * FROM ACCT_CATEGORY ORDER BY CATEGORY_NAME");

            if (dt.Rows.Count == 0) return null;

            foreach (DataRow row in dt.Rows)
            {
                retVal.Add(new CategoryModel
                {
                    CategoryId = Convert.ToInt32(row["CATEGORY_ID"]),
                    CategoryName = row["CATEGORY_NAME"].ToString()
                });
            }

            return retVal;
        }

        public string GetPassword(int accountId)
        {
            return DAL.Kashif.ExecuteScalar("SELECT ACCT_PASSWORD FROM ACCT_MGR WHERE ID = " + accountId).ToString();
        }

        public int InsertNewAccount(AccountModel newAcctModel)
        {
            var encryptedPassword = Encryption.Encrypt(newAcctModel.AccountPassword);
            var notesVal = string.IsNullOrEmpty(newAcctModel.Notes) ? "NULL" : $"'{HelperTools.FormatSqlString(newAcctModel.Notes)}'";
            var acctId = Convert.ToInt32(DAL.Kashif.ExecuteScalar("SELECT ACCT_SEQ.NEXTVAL FROM DUAL"));
            
            _sql = $@"INSERT INTO ACCT_MGR
                      (ID, ACCT_NAME, ACCT_LOGIN_ID, ACCT_PASSWORD, ACCT_NOTES, DATE_CREATED, DATE_MODIFIED, CATEGORY_ID, IS_PASSWORD_ENCRYPTED) 
                      VALUES(
                      {acctId},
                     '{HelperTools.FormatSqlString(newAcctModel.AccountName)}',
                     '{HelperTools.FormatSqlString(newAcctModel.AccountLoginId)}',
                     '{encryptedPassword}',
                      {notesVal},
                      SYSDATE,
                      NULL,
                      {newAcctModel.CategoryId},  
                      'Y')";

            DAL.Kashif.ExecuteNonQuery(_sql);

            return acctId;
        }

        public void UpdateAccount(AccountModel updatedAcctModel)
        {
            var encryptedPassword = Encryption.Encrypt(updatedAcctModel.AccountPassword);
            var notesVal = string.IsNullOrEmpty(updatedAcctModel.Notes)? "NULL" : $"'{HelperTools.FormatSqlString(updatedAcctModel.Notes)}'";

            _sql = $@"UPDATE ACCT_MGR
                      SET ACCT_NAME = '{HelperTools.FormatSqlString(updatedAcctModel.AccountName)}', 
                          ACCT_LOGIN_ID  = '{HelperTools.FormatSqlString(updatedAcctModel.AccountLoginId)}', 
                          ACCT_PASSWORD  = '{encryptedPassword}',     
                          ACCT_NOTES = {notesVal}, 
                          CATEGORY_ID = {updatedAcctModel.CategoryId},
                          DATE_MODIFIED = SYSDATE, 
                          IS_PASSWORD_ENCRYPTED = 'Y'
                      WHERE ID = {updatedAcctModel.AccountId}";

            DAL.Kashif.ExecuteNonQuery(_sql);
        }

        public int InsertNewAccountCategory(string newCategoryName)
        {
            var categoryId = Convert.ToInt32(DAL.Kashif.ExecuteScalar("SELECT ACCT_SEQ.NEXTVAL FROM DUAL"));
            _sql = $@"INSERT INTO ACCT_CATEGORY(CATEGORY_ID, CATEGORY_NAME)
                        VALUES({categoryId}, '{HelperTools.FormatSqlString(newCategoryName)}')";

            DAL.Kashif.ExecuteNonQuery(_sql);

            return categoryId;
        }

        public void UpdateAccountCategory(int categoryId, string newCategoryName)
        {
            _sql = $@"UPDATE ACCT_CATEGORY 
                        SET CATEGORY_NAME = '{HelperTools.FormatSqlString(newCategoryName)}'
                        WHERE CATEGORY_ID = {categoryId}";

            DAL.Kashif.ExecuteNonQuery(_sql);
        }
    }
}
