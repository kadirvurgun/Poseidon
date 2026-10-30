using Npgsql;
using Xunit;
namespace Poseidon.Tests;
public static class TestDatabase {
 public static string Connection=>Validate(Environment.GetEnvironmentVariable("POSEIDON_TEST_POSTGRES")??throw new InvalidOperationException("POSEIDON_TEST_POSTGRES is required."));
 public static string Validate(string connection){
  var database=new NpgsqlConnectionStringBuilder(connection).Database;
  if(database!="poseidon_test" && (database is null || !System.Text.RegularExpressions.Regex.IsMatch(database,@"\Aposeidon_test_[a-z0-9_]+\z")))throw new InvalidOperationException("Refusing integration-test DDL: database must be poseidon_test or poseidon_test_<lowercase disposable suffix>.");
  return connection;
 }
}
public class DatabaseSafetyTests {
 [Theory][InlineData("poseidon_test")][InlineData("poseidon_test_synthetic_001")]
 public void Dedicated_test_database_names_allowed(string database)=>Assert.Equal($"Host=unused;Database={database}",TestDatabase.Validate($"Host=unused;Database={database}"));
 [Theory][InlineData("production")][InlineData("postgres")][InlineData("poseidon_testcustomer")][InlineData("poseidon_test_")]
 public void Non_test_database_names_rejected_before_connecting(string database)=>Assert.Throws<InvalidOperationException>(()=>TestDatabase.Validate($"Host=unused;Database={database}"));
}
