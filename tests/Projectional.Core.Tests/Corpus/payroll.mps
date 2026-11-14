<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="051ad3ff-b3d3-4f28-9534-c957f1107891" name="Payroll">
  <nodes>
    <node id="5ce5a5ce-7862-4d82-98c2-6c755e953674" concept="Test.Rule" name="Payroll">
      <properties>
        <name>Payroll</name>
        <enabled>true</enabled>
      </properties>
      <references role="tag" target="6c04884e-6511-4d25-b1f6-6562958801fd" resolveInfo="Salary" />
      <children role="body">
        <node id="698fdaff-0cb0-4a29-be36-5ba1970b68c3" concept="Test.Body">
          <children role="steps">
            <node id="be68c281-bb6a-4613-8dce-59d1d989ee48" concept="Test.Step">
              <properties>
                <text>Validate the invoice</text>
                <index>1</index>
                <due>2026-01-02</due>
                <status>draft</status>
              </properties>
              <children role="detail">
                <node id="47730055-fb7d-4f65-a56e-b3cd2bf137d8" concept="Test.Step">
                  <properties>
                    <text>check the rounding</text>
                  </properties>
                </node>
              </children>
            </node>
            <node id="3887d7b9-0358-4e20-8df3-da60881449c1" concept="Test.Step">
              <properties>
                <text>Pay</text>
                <index>2</index>
                <status>approved</status>
              </properties>
            </node>
          </children>
          <children role="notes">
            <node id="fadbb32d-0f0b-47ee-99b5-5db698016a8b" concept="Test.Note">
              <properties>
                <text>payroll runs monthly</text>
              </properties>
            </node>
          </children>
        </node>
      </children>
    </node>
    <node id="6c04884e-6511-4d25-b1f6-6562958801fd" concept="Test.Tag" name="Salary">
      <properties>
        <label>Salary</label>
      </properties>
    </node>
  </nodes>
</model>