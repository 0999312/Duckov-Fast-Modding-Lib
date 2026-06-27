using System;

namespace FastModdingLib.Utils
{
    public class Identifier: IEquatable<Identifier>
    {
        private readonly string _domain;
        private readonly string _path;
        
        public string Domain
        {
            get => _domain;
        }
        public string Path
        {
            get => _path;
        }

        private static void isValid(string str, string hint)
        {
            if (str.Contains(":"))
            {
                throw new ArgumentException($":(Colon) is not accepted in {hint}");
            }

            if (str.Equals(""))
            {
                throw new ArgumentException($"{str} must not be empty");
            }

            if (str.Contains(".."))
            {
                throw new ArgumentException($"Double dot is not accepted in {hint}");
            }
            
            if (str.Contains("/"))
            {
                throw new ArgumentException($"/(Forward slash) is not accepted in {hint}");
            }
            
            if (str.Contains("\\"))
            {
                throw new ArgumentException($"\\(Backward slash) is not accepted in {hint}");
            }
        }

        public Identifier(string domain, string path)
        {
            isValid(domain, "domain");
            isValid(path, "path");
            _domain = domain;
            _path = path;
        }

        public Identifier(string raw)
        {
            if (raw == "")
            {
                throw new ArgumentException("Resource Location must not be a empty string");
            }
            string[] splitted = raw.Split(":");
            if (splitted.Length <= 1)
            {
                throw new ArgumentException(":(Colon) missing");
            }
            if (splitted.Length >= 3)
            {
                throw new ArgumentException(":(Colon) too much!");
            }

            string domain = splitted[0];
            string path = splitted[1];
            isValid(domain, "domain");
            isValid(path, "path");
            _domain = domain;
            _path = path;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null) return false;
            return obj is Identifier other &&
                   Equals(other);
        }

        public bool Equals(Identifier other)
        {
            return Path.Equals(other.Path) && Domain.Equals(other.Domain);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Path, Domain);
        }
    }
}