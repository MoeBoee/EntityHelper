using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows;
using System.Data;

namespace EntityHelper
{
    public abstract class EntityDerivate : INotifyPropertyChanged
    {

        public EntityDerivate (String nestedEntityName, String primaryKeyName)
        {
            PrimaryKeyName = primaryKeyName;
            NestedEntityName = nestedEntityName;
        }

        string PrimaryKeyName;
        string NestedEntityName;

        /// <summary>
        /// Speichert, ob die Entität einmal im Status "Added" war. 
        /// Wenn dies der Fall war, darf die Entität beim DoCommit befehl NICHT gelöscht werden, weil die Entität nie in der DB vorhanden war. 
        /// </summary>
        bool wasAdded = false;

        EntityState _state = EntityState.Unchanged;
        /// <summary>
        /// Änderungsstatus des Objekts - Standard = Unchanged
        /// </summary>
        public EntityState State
        {
            get { return _state; }
            protected set
            {
                _state = value;
                if (value == EntityState.Added) wasAdded = true;
                NotifyPropertyChanged("State", true);
                NotifyPropertyChanged("ChangeMark", true);
            }
        }

        /// <summary>
        /// gibt die eingebettete DB-Entität aus this zurück - Reflection - definiert in base-Constrctor
        /// </summary>
        /// 
        //public abstract object Entity { get; protected set; }
        public object Entity
        {
            get
            {
                try
                {
                    var p = this.GetType().GetProperty(NestedEntityName);
                    return p.GetValue(this);
                }
                catch
                {

                    throw new Exception("für den Typ: " + this.GetType().Name + " kann keine EntityProperty " + NestedEntityName + " gefunden werden. Die Property muss exsistieren GET/SET && protected/public sein ");
                }
            }
        }

        /// <summary>
        /// zeigt mit einem * an, ob es seit dem letzten Speichern eine Änderung an der Entity gegeben hat. 
        /// von State abhängig
        /// </summary>
        public String ChangeMark
        {
            get
            {
                switch (State)
                {
                    case EntityState.Detached:
                        return "";
                    case EntityState.Unchanged:
                        return "";
                    case EntityState.Added:
                        return "*";
                    case EntityState.Deleted:
                        return "*";
                    case EntityState.Modified:
                        return "*";
                    default:
                        return "";
                }
            }
        }

        //public abstract void Delete();

        public abstract void Save(DbContext c = null);
              
        public void SetEntityState(DbContext context)
        {
            context.Entry(Entity).State = State;
        }

        protected void CompleteEntityState()
        {
            //bei ADDED ändert sich nach dem speichern 
            NotifyPropertyChanged(PrimaryKeyName);

            //setzt nach dem speichern den status zurück
            if (State != EntityState.Deleted) State = EntityState.Unchanged;
        }

        //public EntityChangeLogger ChangeLogger = new EntityChangeLogger();

        /// <summary>
        /// schreibt den zustand der DB-Entität zur DB zurück. - Insert, modify, delete
        /// bei Unchanged wird keine Datenbank-Aktion durchgeführt
        /// </summary>
        /// <param name="entity">Stellt die Entität dar, die im DB-Context steht. z.B. ein tbl-Object</param>
        /// <param name="primaryKeyName">PrimärSchlüssel Name, wenn gesetzt wird nach dem Speichern NotifyPropertyChanged für diese Peroperty getriggert </param>
        protected void doDbCommit(DbContext context)
        {
            if (State != EntityState.Unchanged)
            {
                if (State == EntityState.Deleted && wasAdded )
                {
                    //Ein glöschte Entity, die Added war wurde nie in der DB gespeichert und kann somit auch nicht aus der DB gelöscht werden!
                } else
                {
                    context.Entry(Entity).State = State;
                    context.SaveChanges();

                    //bei ADDED ändert sich nach dem speichern 
                    NotifyPropertyChanged(PrimaryKeyName);

                    //setzt nach dem speichern den status zurück
                    if (State != EntityState.Deleted) State = EntityState.Unchanged;
                    wasAdded = false;
                }
                
            }
        }              


        /// <summary>
        /// Läd die Entity aus der DB nochmals
        /// </summary>
        /// <param name="context">Entity DbContext indem die entity gebunden ist</param>
        /// <param name="entity">Stellt die Entität dar, die im DB-Context steht. z.B. ein tbl-Object</param>
        protected void doDBReload(DbContext context)
        {
            if (State != EntityState.Unchanged)
            {
                context.Entry(Entity).State = EntityState.Unchanged;
                context.Entry(Entity).Reload();
                State = EntityState.Unchanged;

                //Dienst dazu, dass für alle Properties auch ein ChangeEvent getriggert wird und damit auch die GUI aktuell bleibt
                this.GetType().GetProperties().ToList().ForEach(prop => NotifyPropertyChanged(prop.Name, true));
                //ChangeLogger.Clear();                    
            }
        }

        /// <summary>
        /// Führt eine Transaktion mit Rollback aus. Dient zum Test, ob die Entity gespeichert werden kann
        /// </summary>
        /// <param name="context">Entity DbContext indem die entity gebunden ist</param>
        protected void doDbCheck(DbContext context)
        {
            if (State != EntityState.Unchanged)
            {
                using (var t = context.Database.BeginTransaction())
                {
                    try
                    {
                        context.Entry(Entity).State = State;
                        context.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                    finally
                    {
                        t.Rollback();
                    }                    
                }
            }
        }

        /// <summary>
        /// markiert eine Entität als gelöscht!
        /// WICHTIG: Um eine Entität final zu löschen ist ein Save notwendig!
        /// </summary>
        /// <param name="entity"></param>
        public virtual void MarkToDelete()
        {
            //auch Added Items müssen als gelöscht markiert werden könnnen. 
            //Exception muss bei doDbCommit() abgefangen werden, da eine Added-Item NICHT aus der DB gelsöcht werden kann!!
            State = EntityState.Deleted;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Triggert PropertyChanged-Event für GUI-Update
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="avoidChangeEntityChangeFlag"> setzte auf 'true' wenn Änderungen einer Property eintreten aber keine Änderung im DB-Bestand bewirken --> für Indirekte Object-Änderugnen</param>
        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "", bool avoidChangeEntityChangeFlag = false)
        {
            //Update wird getriggert. --> avoidChange of EntityChangeFlag ==> eine Variable ändert zwar ihren Wert, 
            //hat aber keine Auswirkung auf den DB-Bestand

            //Change 20220711: Reiehnfolge geändert: erst wird der State geändert und dann erst das Event getriggert
            if (!avoidChangeEntityChangeFlag)
            {
                if (
                    propertyName != PrimaryKeyName && 
                    propertyName != "State" && 
                    State != EntityState.Added && 
                    State != EntityState.Deleted)
                        State = EntityState.Modified;
                if (propertyName != "State" && propertyName != "ID")
                {
                    //ChangeLogger.AddChange(this, propertyName);
                }

            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        
    }

}
